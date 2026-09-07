using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Dapper;

namespace AISLiveTracking.API.Data.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly DatabaseConnection _database;

    public ApiKeyRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task<ApiKey?> GetByHashAsync(byte[] keyHash)
    {
        const string sql = """
            SELECT
                id AS Id,
                client_id AS ClientId,
                key_hash AS KeyHash,
                key_prefix AS KeyPrefix,
                is_active AS IsActive,
                created_utc AS CreatedUtc,
                revoked_utc AS RevokedUtc
            FROM api_keys
            WHERE key_hash = @KeyHash
        """;

        using var connection = _database.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<ApiKey>(
            sql,
            new { KeyHash = keyHash });
    }
}