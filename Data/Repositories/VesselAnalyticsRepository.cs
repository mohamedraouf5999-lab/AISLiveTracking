using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Dapper;

namespace AISLiveTracking.API.Data.Repositories;

public class VesselAnalyticsRepository : IVesselAnalyticsRepository
{
    private readonly DatabaseConnection _database;

    public VesselAnalyticsRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<Position>> GetTrackAsync(
        long mmsi,
        DateTime from,
        DateTime to)
    {
        using var connection = _database.CreateConnection();

        const string sql = """
            SELECT
                id AS Id,
                mmsi AS Mmsi,
                latitude AS Latitude,
                longitude AS Longitude,
                sog AS Sog,
                cog AS Cog,
                true_heading AS TrueHeading,
                nav_status AS NavStatus,
                rate_of_turn AS RateOfTurn,
                position_accuracy AS PositionAccuracy,
                msg_timestamp_utc AS MsgTimestampUtc,
                received_utc AS ReceivedUtc
            FROM positions
            WHERE mmsi = @Mmsi
              AND msg_timestamp_utc >= @From
              AND msg_timestamp_utc < @To
            ORDER BY msg_timestamp_utc ASC, id ASC;
            """;

        var positions = await connection.QueryAsync<Position>(
            sql,
            new
            {
                Mmsi = mmsi,
                From = from,
                To = to
            });

        return positions.AsList();
    }
}