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

var builder = WebApplication.CreateBuilder(args);

// bắt buộc có Secret
builder.Configuration.AddJsonFile("appsettings.Secret.json", optional: false, reloadOnChange: true);

// Options
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<OtpSettings>(builder.Configuration.GetSection("Otp"));

// EF Core
var cs = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(o => o.UseMySql(cs, ServerVersion.AutoDetect(cs)));

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Ok(new { ok = true, time = DateTime.UtcNow }));

app.Run();
