using AISLiveTracking.API.Data;
using AISLiveTracking.API.Data.Repositories;
using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);



builder.Services.AddHostedService<AisIngestionBackgroundService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<DatabaseConnection>();
builder.Services.AddScoped<IVesselRepository, VesselRepository>();




var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();


app.Run();