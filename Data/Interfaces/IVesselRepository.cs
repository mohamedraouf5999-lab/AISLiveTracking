using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Interfaces;

public interface IVesselRepository
{
    Task<Vessel?> GetByMmsiAsync(long mmsi);

    Task UpsertAsync(Vessel vessel);

    Task<Vessel?> GetByImoAsync(int imo);
}