using AISLiveTracking.API.Data;
using AISLiveTracking.API.Data.Repositories;
using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.BackgroundServices;
using AISLiveTracking.API.Data.Services;
using AISLiveTracking.API.Services;
using AISLiveTracking.API.Services.Interfaces;



var builder = WebApplication.CreateBuilder(args);



builder.Services.AddHostedService<AisIngestionBackgroundService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<DatabaseConnection>();
builder.Services.Configure<AnalyticsOptions>(
    builder.Configuration.GetSection("Analytics"));
builder.Services.AddScoped<IVesselRepository, VesselRepository>();
builder.Services.AddScoped<IPositionRepository, PositionRepository>();
builder.Services.AddScoped<IIdentifierResolver, IdentifierResolver>();
builder.Services.AddScoped<ILatestPositionRepository, LatestPositionRepository>();
builder.Services.AddScoped<IPositionHistoryRepository, PositionHistoryRepository>();
builder.Services.AddScoped<IVesselAnalyticsRepository, VesselAnalyticsRepository>();
builder.Services.AddScoped<IVesselAnalyticsService, VesselAnalyticsService>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();


app.Run();