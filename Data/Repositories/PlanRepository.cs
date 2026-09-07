using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Dapper;

namespace AISLiveTracking.API.Data.Repositories;

public class PlanRepository : IPlanRepository
{
    private readonly DatabaseConnection _database;

    public PlanRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task<Plan?> GetByIdAsync(int id)
    {
        const string sql = """
            SELECT
                id AS Id,
                name AS Name,
                rate_limit_per_minute AS RateLimitPerMinute,
                monthly_quota AS MonthlyQuota,
                max_history_days AS MaxHistoryDays,
                analytics_enabled AS AnalyticsEnabled
            FROM plans
            WHERE id = @Id
        """;

        using var connection = _database.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<Plan>(
            sql,
            new { Id = id });
    }
}