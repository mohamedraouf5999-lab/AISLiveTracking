using System.Security.Cryptography;
using System.Text;
using AISLiveTracking.API.Data.Interfaces;

namespace AISLiveTracking.API.Services;

public class ApiKeyService
{
    private readonly IApiKeyRepository _apiKeyRepository;

    public ApiKeyService(IApiKeyRepository apiKeyRepository)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    public async Task<string> CreateAsync(Guid clientId)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);

        var rawApiKey = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var keyPrefix = rawApiKey[..8];

        var keyHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(rawApiKey));

        await _apiKeyRepository.CreateAsync(
            clientId,
            keyHash,
            keyPrefix);

        return rawApiKey;
    }
}