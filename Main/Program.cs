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
    using Main.Middleware;
    using Main.Models;
    using Microsoft.AspNetCore.Mvc;
    using System.Linq;

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


    //fb
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
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
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

    // DI Helpers + Services
    builder.Services.AddSingleton<IMailSender, MailSender>();
    builder.Services.AddSingleton<IOtpStore, RedisOtpStore>();
    builder.Services.AddScoped<IJwtTokenFactory, JwtTokenFactory>();
    builder.Services.AddScoped<IAuthService, AuthService>();


const string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.AllowAnyOrigin()
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
    app.UseSwagger();
    app.UseSwaggerUI();

app.UseCors(MyAllowSpecificOrigins);
app.UseAuthentication();
app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/", () => Results.Ok(new { ok = true, time = DateTime.UtcNow }));

    app.Run();
