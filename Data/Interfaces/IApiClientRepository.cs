using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Interfaces;

public interface IApiClientRepository
{
    Task<ApiClient?> GetByIdAsync(Guid id);
}