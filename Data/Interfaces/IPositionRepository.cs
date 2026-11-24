using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Interfaces;

public interface IPositionRepository
{
    Task InsertAsync(PositionReport position, DateTime messageTime);

    Task<Position?> GetLatestByMmsiAsync(long mmsi);

    Task<PositionHistoryResult> GetPositionsAsync(
        DateTime? from,
        DateTime? to,
        double? minLat,
        double? maxLat,
        double? minLon,
        double? maxLon,
        long[]? mmsis,
        int[]? navStatusCodes,
        double? minSog,
        double? maxSog,
        string sort,
        int limit,
        string? cursor);
}