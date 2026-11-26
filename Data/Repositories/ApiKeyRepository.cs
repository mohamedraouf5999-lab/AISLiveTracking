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

    public async Task<Guid> CreateAsync(
        Guid clientId,
        byte[] keyHash,
        string keyPrefix)
    {
        const string sql = """
            INSERT INTO api_keys
            (
                client_id,
                key_hash,
                key_prefix,
                is_active
            )
            OUTPUT INSERTED.id
            VALUES
            (
                @ClientId,
                @KeyHash,
                @KeyPrefix,
                1
            )
            """;

        using var connection = _database.CreateConnection();

        return await connection.ExecuteScalarAsync<Guid>(
            sql,
            new
            {
                ClientId = clientId,
                KeyHash = keyHash,
                KeyPrefix = keyPrefix
            });
    }

    public async Task<bool> RevokeAsync(Guid id)
    {
        const string sql = """
            UPDATE api_keys
            SET
                is_active = 0,
                revoked_utc = COALESCE(
                    revoked_utc,
                    SYSUTCDATETIME())
            WHERE id = @Id
            """;

        using var connection = _database.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(
            sql,
            new { Id = id });

        return rowsAffected > 0;
    }
}