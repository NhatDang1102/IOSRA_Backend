using System.Security.Claims;
using System.Text;
using Contract.DTOs.Settings;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Main.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repository.DBContext;
using Service.Helpers;
using Service.Interfaces;
using StackExchange.Redis;

namespace Main
{
    public static class AppInjection
    {
        public static IServiceCollection AddMainAppServices(this IServiceCollection services, IConfiguration configuration, string corsPolicyName)
        {
            // Options
            services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
            services.Configure<OtpSettings>(configuration.GetSection("Otp"));

            // EF Core
            var cs = configuration.GetConnectionString("Default");
            services.AddDbContext<AppDbContext>(o => o.UseMySql(cs, ServerVersion.AutoDetect(cs)));

            // Firebase
            var fb = configuration.GetSection("Firebase");
            services.AddSingleton(provider =>
            {
                var path = fb["CredentialPath"];
                var options = new AppOptions
                {
                    Credential = GoogleCredential.FromFile(path),
                    ProjectId = fb["ProjectId"]
                };
                return FirebaseApp.DefaultInstance ?? FirebaseApp.Create(options);
            });
            services.AddSingleton<IFirebaseAuthVerifier, FirebaseAuthVerifier>();

            // JWT
            var jwt = configuration.GetSection("Jwt");
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwt["Issuer"],
                        ValidAudience = jwt["Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = ClaimTypes.Name
                    };

                    o.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = ctx =>
                        {
                            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                                                  .CreateLogger("JWT");
                            logger.LogError(ctx.Exception, "JWT authentication failed");
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            var claimsIdentity = context.Principal?.Identity as ClaimsIdentity;
                            if (claimsIdentity is null) return Task.CompletedTask;

                            var rolesArray = context.Principal?.Claims
                                .Where(c => c.Type == "roles")
                                .Select(c => c.Value)
                                .ToList();

                            if (rolesArray is { Count: > 0 })
                            {
                                foreach (var role in rolesArray.SelectMany(r => r.Split(',', StringSplitOptions.RemoveEmptyEntries)))
                                {
                                    if (!claimsIdentity.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value.Equals(role, StringComparison.OrdinalIgnoreCase)))
                                        claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
                                }
                            }

                            var singleRole = context.Principal?.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
                            if (!string.IsNullOrWhiteSpace(singleRole) &&
                                !claimsIdentity.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value.Equals(singleRole, StringComparison.OrdinalIgnoreCase)))
                            {
                                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, singleRole));
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            // Redis
            var r = configuration.GetSection("Redis");
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect($"{r["Host"]}:{r["Port"]}"));

            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy(name: corsPolicyName, policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // Authorization (ADMIN policy)
            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", p => p.RequireRole("ADMIN"));
            });

            // Controllers + Endpoints
            services.AddControllers();
            services.AddEndpointsApiExplorer();

            // Swagger + Bearer
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Nhập JWT theo dạng: Bearer {token}",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // ApiBehavior - Validation error shape
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

                    var response = ErrorResponse.From("ValidationFailed", "Dữ liệu không hợp lệ.", errors);
                    return new BadRequestObjectResult(response);
                };
            });

            return services;
        }
    }
}
