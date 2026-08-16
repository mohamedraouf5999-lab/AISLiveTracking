using Dapper;
using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Repositories;

public class LatestPositionRepository : ILatestPositionRepository
{
    private readonly DatabaseConnection _databaseConnection;

    public LatestPositionRepository(DatabaseConnection databaseConnection)
    {
        _databaseConnection = databaseConnection;
    }

    public async Task<Position?> GetLatestByMmsiAsync(long mmsi)
    {
        const string sql = @"
    SELECT TOP (1)
        mmsi AS Mmsi,
        latitude AS Latitude,
        longitude AS Longitude,
        sog AS Sog,
        cog AS Cog,
        true_heading AS TrueHeading,
        nav_status AS NavStatus,
        position_accuracy AS PositionAccuracy,
        rate_of_turn AS RateOfTurn,
        msg_timestamp_utc AS MsgTimestampUtc,
        received_utc AS ReceivedUtc
    FROM positions
    WHERE mmsi = @Mmsi
    ORDER BY msg_timestamp_utc DESC;";

        using var connection = _databaseConnection.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<Position>(
            sql,
            new { Mmsi = mmsi });
    }
}