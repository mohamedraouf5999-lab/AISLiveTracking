using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Interfaces;

public interface IPositionHistoryRepository
{
    Task<PositionHistoryResult> GetHistoryAsync(
        long mmsi,
        DateTime? from,
        DateTime? to,
        double? minLat,
        double? maxLat,
        double? minLon,
        double? maxLon,
        string? navStatus,
        double? minSog,
        double? maxSog,
        string sort,
        int limit,
        string? cursor);
}