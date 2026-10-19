using System.Security.Claims;
using AISLiveTracking.API.Data.Interfaces;

namespace AISLiveTracking.API.Middleware;

public class ApiUsageMiddleware
{
    private readonly RequestDelegate _next;

    public ApiUsageMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IApiUsageRepository apiUsageRepository,
        IPlanRepository planRepository)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        if (!context.User.HasClaim(c => c.Type == "plan_id"))
        {
            await _next(context);
            return;
        }

        var apiKeyIdValue =
            context.User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(apiKeyIdValue, out var apiKeyId))
        {
            await _next(context);
            return;
        }

        var planIdValue =
            context.User.FindFirst("plan_id")?.Value;

        if (!int.TryParse(planIdValue, out var planId))
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            await context.Response.WriteAsJsonAsync(new
            {
                message = "Plan information is missing."
            });

            return;
        }

        var plan =
            await planRepository.GetByIdAsync(planId);

        if (plan == null)
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            await context.Response.WriteAsJsonAsync(new
            {
                message = "API plan not found."
            });

            return;
        }

        if (plan.MonthlyQuota.HasValue)
        {
            var now = DateTime.UtcNow;

            var monthStart = new DateTime(
                now.Year,
                now.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

            var nextMonth = monthStart.AddMonths(1);

            var monthlyUsage =
                await apiUsageRepository.GetMonthlyUsageAsync(
                    apiKeyId,
                    monthStart,
                    nextMonth);

            if (monthlyUsage >= plan.MonthlyQuota.Value)
            {
                context.Response.StatusCode =
                    StatusCodes.Status429TooManyRequests;

                context.Response.Headers.RetryAfter = "3600";

                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Monthly API quota exceeded."
                });

                return;
            }
        }

        await _next(context);

        await apiUsageRepository.RecordAsync(
            apiKeyId,
            context.Request.Path.ToString(),
            context.Response.StatusCode);
    }
}