using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Interfaces;

public interface ILatestPositionRepository
{
    Task<Position?> GetLatestByMmsiAsync(long mmsi);
}