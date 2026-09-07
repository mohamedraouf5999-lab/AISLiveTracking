using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Dapper;

namespace AISLiveTracking.API.Data.Repositories;

public class ApiClientRepository : IApiClientRepository
{
    private readonly DatabaseConnection _database;

    public ApiClientRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task<ApiClient?> GetByIdAsync(Guid id)
    {
        const string sql = """
            SELECT
                id AS Id,
                name AS Name,
                plan_id AS PlanId,
                created_utc AS CreatedUtc
            FROM api_clients
            WHERE id = @Id
        """;

        using var connection = _database.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<ApiClient>(
            sql,
            new { Id = id });
    }
}