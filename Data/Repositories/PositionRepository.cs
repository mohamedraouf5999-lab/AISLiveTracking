using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Dapper;

namespace AISLiveTracking.API.Data.Repositories;

public class PositionRepository : IPositionRepository
{
    private readonly DatabaseConnection _database;

    public PositionRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task InsertAsync(PositionReport position, DateTime messageTime)
    {
        using var connection = _database.CreateConnection();

        const string sql = """
            INSERT INTO positions
            (
                mmsi,
                latitude,
                longitude,
                sog,
                cog,
                true_heading,
                nav_status,
                rate_of_turn,
                position_accuracy,
                msg_timestamp_utc
            )
            VALUES
            (
                @UserID,
                @Latitude,
                @Longitude,
                @Sog,
                @Cog,
                @TrueHeading,
                @NavigationalStatus,
                @RateOfTurn,
                @PositionAccuracy,
                @MessageTime
            );
            """;

        await connection.ExecuteAsync(sql, new
        {
            position.UserID,
            position.Latitude,
            position.Longitude,
            position.Sog,
            position.Cog,
            position.TrueHeading,
            position.NavigationalStatus,
            position.RateOfTurn,
            position.PositionAccuracy,
            MessageTime = messageTime
        });
    }
    public async Task<Position?> GetLatestByMmsiAsync(long mmsi)
    {
        using var connection = _database.CreateConnection();

        const string sql = """
        SELECT TOP (1)
            id,
            mmsi,
            latitude,
            longitude,
            sog,
            cog,
            true_heading,
            nav_status,
            rate_of_turn,
            position_accuracy,
            msg_timestamp_utc,
            received_utc
        FROM positions
        WHERE mmsi = @mmsi
        ORDER BY msg_timestamp_utc DESC;
        """;

        return await connection.QueryFirstOrDefaultAsync<Position>(
            sql,
            new { mmsi });
    }
}