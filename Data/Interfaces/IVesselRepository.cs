using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Interfaces;

public interface IVesselRepository
{
    Task<Vessel?> GetByMmsiAsync(long mmsi);
}