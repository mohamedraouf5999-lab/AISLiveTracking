using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByHashAsync(byte[] keyHash);

    Task CreateAsync(
        Guid clientId,
        byte[] keyHash,
        string keyPrefix);
}