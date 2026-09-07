using AISLiveTracking.API.Data.Interfaces;
using Dapper;

namespace AISLiveTracking.API.Data.Repositories;

public class ApiUsageRepository : IApiUsageRepository
{
    private readonly DatabaseConnection _database;

    public ApiUsageRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task<long> GetMonthlyUsageAsync(
        Guid apiKeyId,
        DateTime fromUtc,
        DateTime toUtc)
    {
        const string sql = """
            SELECT COUNT_BIG(*)
            FROM api_usage
            WHERE api_key_id = @ApiKeyId
              AND occurred_utc >= @FromUtc
              AND occurred_utc < @ToUtc
        """;

        using var connection = _database.CreateConnection();

        return await connection.ExecuteScalarAsync<long>(
            sql,
            new
            {
                ApiKeyId = apiKeyId,
                FromUtc = fromUtc,
                ToUtc = toUtc
            });
    }

    public async Task RecordAsync(
        Guid apiKeyId,
        string endpoint,
        int statusCode)
    {
        const string sql = """
            INSERT INTO api_usage
            (
                api_key_id,
                endpoint,
                status_code,
                occurred_utc
            )
            VALUES
            (
                @ApiKeyId,
                @Endpoint,
                @StatusCode,
                SYSUTCDATETIME()
            )
        """;

        using var connection = _database.CreateConnection();

        await connection.ExecuteAsync(
            sql,
            new
            {
                ApiKeyId = apiKeyId,
                Endpoint = endpoint,
                StatusCode = statusCode
            });
    }
}