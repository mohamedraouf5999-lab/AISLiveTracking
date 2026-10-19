using AISLiveTracking.API.Authentication;
using AISLiveTracking.API.BackgroundServices;
using AISLiveTracking.API.Data;
using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Data.Repositories;
using AISLiveTracking.API.Data.Services;
using AISLiveTracking.API.Models;
using AISLiveTracking.API.Services;
using AISLiveTracking.API.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using AISLiveTracking.API.Middleware;

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

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT Bearer token."
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
        }] = Array.Empty<string>(),

        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        }] = Array.Empty<string>()
    });
});

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Smart";
        options.DefaultChallengeScheme = "Smart";
    })
    .AddPolicyScheme("Smart", "API Key or JWT", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.ContainsKey("X-API-Key"))
            {
                return "ApiKey";
            }

            if (context.Request.Headers.TryGetValue(
                "Authorization",
                out var authorizationHeader))
            {
                var authorization = authorizationHeader.ToString();

                if (authorization.StartsWith(
                    "ApiKey ",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return "ApiKey";
                }

                if (authorization.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return "Bearer";
                }
            }

            return "Bearer";
        };
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        "ApiKey",
        options => { })
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters =
            new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],

                IssuerSigningKey =
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(
                            builder.Configuration["Jwt:Key"]!))
            };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<DatabaseConnection>();

builder.Services.Configure<AnalyticsOptions>(
    builder.Configuration.GetSection("Analytics"));

builder.Services.AddScoped<IVesselRepository, VesselRepository>();
builder.Services.AddScoped<IPositionRepository, PositionRepository>();

builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
builder.Services.AddScoped<IApiClientRepository, ApiClientRepository>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IApiUsageRepository, ApiUsageRepository>();
builder.Services.AddScoped<ApiKeyService>();

builder.Services.AddScoped<IIdentifierResolver, IdentifierResolver>();
builder.Services.AddScoped<ILatestPositionRepository, LatestPositionRepository>();
builder.Services.AddScoped<IPositionHistoryRepository, PositionHistoryRepository>();
builder.Services.AddScoped<IVesselAnalyticsRepository, VesselAnalyticsRepository>();
builder.Services.AddScoped<IVesselAnalyticsService, VesselAnalyticsService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<AdminBootstrapService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";

        await ValueTask.CompletedTask;
    };

    options.AddPolicy("ApiKeyRateLimit", context =>
    {
        var apiKeyId =
            context.User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)
            ?.Value;

        var partitionKey =
            string.IsNullOrWhiteSpace(apiKeyId)
                ? "anonymous"
                : apiKeyId;

        var planIdValue =
            context.User.FindFirst("plan_id")?.Value;

        var planId =
            int.TryParse(planIdValue, out var parsedPlanId)
                ? parsedPlanId
                : 1;

        var planRepository =
            context.RequestServices.GetRequiredService<IPlanRepository>();

        var plan =
            planRepository.GetByIdAsync(planId)
                .GetAwaiter()
                .GetResult();

        var permitLimit =
            plan?.RateLimitPerMinute ?? 30;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var adminBootstrapService =
        scope.ServiceProvider.GetRequiredService<AdminBootstrapService>();

    await adminBootstrapService.EnsureAdminAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.UseMiddleware<ApiUsageMiddleware>();

app.MapControllers();

app.Run();