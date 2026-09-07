using AISLiveTracking.API.Authentication;
using AISLiveTracking.API.BackgroundServices;
using AISLiveTracking.API.Data;
using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Data.Repositories;
using AISLiveTracking.API.Data.Services;
using AISLiveTracking.API.Services;
using AISLiveTracking.API.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<AisIngestionBackgroundService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-API-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Scheme = "ApiKey",
        Description = "Enter your API key."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "ApiKey"
            }
        }] = Array.Empty<string>()
    });
});

builder.Services
    .AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        "ApiKey",
        options => { });

builder.Services.AddAuthorization();

builder.Services.AddScoped<DatabaseConnection>();

builder.Services.Configure<AnalyticsOptions>(
    builder.Configuration.GetSection("Analytics"));

builder.Services.AddScoped<IVesselRepository, VesselRepository>();
builder.Services.AddScoped<IPositionRepository, PositionRepository>();
builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();