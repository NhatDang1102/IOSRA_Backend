using System;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Secret.json", optional: true, reloadOnChange: true);

var cs = builder.Configuration.GetConnectionString("Default");

builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.Run();
