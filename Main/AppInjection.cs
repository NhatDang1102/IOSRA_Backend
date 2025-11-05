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
            services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
            services.Configure<OtpSettings>(configuration.GetSection("Otp"));
            services.Configure<CloudinarySettings>(configuration.GetSection("CloudinarySettings"));
            services.Configure<CloudflareR2Settings>(configuration.GetSection("CloudflareR2"));
            services.Configure<OpenAiSettings>(configuration.GetSection("OpenAi"));
            services.AddMemoryCache();

            var connectionString = configuration.GetConnectionString("Default");
            services.AddDbContext<AppDbContext>(o => o.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

            var firebaseSection = configuration.GetSection("Firebase");
            services.AddSingleton(provider =>
            {
                var path = firebaseSection["CredentialPath"];
                var options = new AppOptions
                {
                    Credential = GoogleCredential.FromFile(path),
                    ProjectId = firebaseSection["ProjectId"]
                };
                return FirebaseApp.DefaultInstance ?? FirebaseApp.Create(options);
            });
            services.AddSingleton<IFirebaseAuthVerifier, FirebaseAuthVerifier>();

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
                        NameClaimType = "username"
                    };

                    o.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                                .CreateLogger("JWT");
                            logger.LogError(context.Exception, "JWT authentication failed");
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = async context =>
                        {
                            var identity = context.Principal?.Identity as ClaimsIdentity;
                            if (identity is null)
                            {
                                return;
                            }

                            var rolesClaim = context.Principal?.Claims
                                .Where(c => c.Type == "roles")
                                .Select(c => c.Value)
                                .ToList();

                            if (rolesClaim is { Count: > 0 })
                            {
                                foreach (var role in rolesClaim.SelectMany(r => r.Split(',', StringSplitOptions.RemoveEmptyEntries)))
                                {
                                    if (!identity.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value.Equals(role, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        identity.AddClaim(new Claim(ClaimTypes.Role, role));
                                    }
                                }
                            }

                            var singleRole = context.Principal?.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
                            if (!string.IsNullOrWhiteSpace(singleRole) &&
                                !identity.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value.Equals(singleRole, StringComparison.OrdinalIgnoreCase)))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, singleRole));
                            }

                            var jti = context.Principal?.FindFirst("jti")?.Value;
                            if (!string.IsNullOrEmpty(jti))
                            {
                                var blacklist = context.HttpContext.RequestServices.GetRequiredService<IJwtBlacklistService>();
                                if (await blacklist.IsBlacklistedAsync(jti))
                                {
                                    context.Fail("Token has been revoked (blacklisted).");
                                }
                            }
                        }
                    };
                });

            var redis = configuration.GetSection("Redis");
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect($"{redis["Host"]}:{redis["Port"]}"));

            services.AddCors(options =>
            {
                options.AddPolicy(corsPolicyName, policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                });
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", p => p.RequireRole("ADMIN"));
            });

            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Enter JWT as: Bearer {token}",
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
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

                    var response = ErrorResponse.From("VALIDATION_FAILED", "Validation failed.", errors);
                    return new BadRequestObjectResult(response);
                };
            });

            return services;
        }
    }
}
