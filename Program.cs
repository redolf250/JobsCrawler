using Crawler.Data;
using Crawler.Service;
using Hangfire;
using HangfireBasicAuthenticationFilter;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IJobsService, GhanaJobsService>();

Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
    .WriteTo.File("logs/crawler.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddHangfire(x =>
    x.UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfireServer();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Crawler API",
        Description = "Jobs website crawler API",
        TermsOfService = new Uri("https://github.com/redolf250"),
        Contact = new OpenApiContact
        {
            Name = "Asamaning Redolf",
            Email = "redolkendrick@gmail.com",
            Url = new Uri("https://github.com/redolf250")
        }
    })
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    DashboardTitle = "Crawler",
    DisplayStorageConnectionString = false,
    DarkModeEnabled = true,
    Authorization = new[]
    {
        new HangfireCustomBasicAuthenticationFilter
        {
            Pass = "123456",
            User = "redolf@250"
        }
    }
});
app.MapHangfireDashboard();

app.Run();