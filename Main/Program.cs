using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Repository.DBContext;
using Repository.Interfaces;
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
using Main;            
using Repository;        
using Service;
using Repository.Utils;        

var builder = WebApplication.CreateBuilder(args);

var env = builder.Environment;
builder.Configuration
    .AddJsonFile("appsettings.Secret.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.Secret.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);

const string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

// === App-level registrations (Options, Db, Firebase, JWT, Redis, CORS, Swagger, MVC, Authorization) ===
builder.Services.AddMainAppServices(builder.Configuration, MyAllowSpecificOrigins);

// === Repository layer ===
builder.Services.AddRepositoryServices();

// === Service layer ===
builder.Services.AddServiceServices();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Ok(new { ok = true, time = TimezoneConverter.VietnamNow }));

app.Run();
