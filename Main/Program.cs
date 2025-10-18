using Microsoft.EntityFrameworkCore;
using Repository.DBContext;

var builder = WebApplication.CreateBuilder(args);

// CHỈ đọc appsettings.Secret.json (bắt buộc phải có)
builder.Configuration
    .AddJsonFile("appsettings.Secret.json", optional: false, reloadOnChange: true);

var cs = builder.Configuration.GetConnectionString("Default");

// DbContext (Pomelo)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(cs, ServerVersion.AutoDetect(cs)));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/", () => Results.Ok(new { ok = true, time = DateTime.UtcNow }));

app.Run();
