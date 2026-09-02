using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Services.Interfaces;

public interface IVesselAnalyticsService
{
    Task<VesselAnalyticsResult> GetAnalyticsAsync(
        long mmsi,
        DateTime from,
        DateTime to);
}