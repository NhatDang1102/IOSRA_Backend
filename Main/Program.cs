using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Repository.DBContext;
using Repository.Interfaces;
using Repository.Repositories;
using Service.Implementations;
using Service.Interfaces;
using Service.Helpers;
using StackExchange.Redis;
using System.Text;
using Contract.DTOs.Settings;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Main.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using Service.Services;
using Main.Middleware;

var builder = WebApplication.CreateBuilder(args);

var env = builder.Environment;
builder.Configuration
    .AddJsonFile("appsettings.Secret.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.Secret.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Options
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<OtpSettings>(builder.Configuration.GetSection("Otp"));

// EF Core
var cs = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(o => o.UseMySql(cs, ServerVersion.AutoDetect(cs)));

// Firebase
var fb = builder.Configuration.GetSection("Firebase");
builder.Services.AddSingleton(provider =>
{
    var path = fb["CredentialPath"];
    var options = new AppOptions
    {
        Credential = GoogleCredential.FromFile(path),
        ProjectId = fb["ProjectId"]
    };
    return FirebaseApp.DefaultInstance ?? FirebaseApp.Create(options);
});
builder.Services.AddSingleton<IFirebaseAuthVerifier, FirebaseAuthVerifier>();

// JWT
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

            // để ASP.NET map đúng role
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };

        // Log lỗi auth + map role từ "role"/"roles"
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
var r = builder.Configuration.GetSection("Redis");
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect($"{r["Host"]}:{r["Port"]}"));

// DI Repos
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IReaderRepository, ReaderRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IAccountRoleRepository, AccountRoleRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();

// DI Helpers + Services
builder.Services.AddSingleton<IMailSender, MailSender>();
builder.Services.AddSingleton<IOtpStore, RedisOtpStore>();
builder.Services.AddScoped<IJwtTokenFactory, JwtTokenFactory>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();

const string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins, policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

//Authorization (policy cho ADMIN)
builder.Services.AddAuthorization(options =>
{
    // Policy dùng ở controller: chỉ cho phép role ADMIN
    options.AddPolicy("AdminOnly", p => p.RequireRole("ADMIN"));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

//Swagger + Bearer
builder.Services.AddSwaggerGen(c =>
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

builder.Services.Configure<ApiBehaviorOptions>(options =>
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

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Ok(new { ok = true, time = DateTime.UtcNow }));

app.Run();
