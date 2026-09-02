using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Interfaces;

public interface IVesselAnalyticsRepository
{
    Task<IReadOnlyList<Position>> GetTrackAsync(
        long mmsi,
        DateTime from,
        DateTime to);
}