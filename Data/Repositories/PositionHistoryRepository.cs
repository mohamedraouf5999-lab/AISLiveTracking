using Dapper;
using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;

namespace AISLiveTracking.API.Data.Repositories;

public class PositionHistoryRepository : IPositionHistoryRepository
{
    private readonly DatabaseConnection _database;

    public PositionHistoryRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task<PositionHistoryResult> GetHistoryAsync(
        long mmsi,
        DateTime? from,
        DateTime? to,
        double? minLat,
        double? maxLat,
        double? minLon,
        double? maxLon,
        string? navStatus,
        double? minSog,
        double? maxSog,
        string sort,
        int limit,
        string? cursor)
    {
        var isAscending = string.Equals(
            sort,
            "asc",
            StringComparison.OrdinalIgnoreCase);

        DateTime? cursorTimestamp = null;
        int? cursorId = null;

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var parts = cursor.Split('|', 2);

            if (parts.Length != 2 ||
                !DateTime.TryParse(parts[0], out var parsedTimestamp) ||
                !int.TryParse(parts[1], out var parsedId))
            {
                throw new ArgumentException("Invalid cursor.");
            }

            cursorTimestamp = parsedTimestamp;
            cursorId = parsedId;
        }

        var cursorCondition = isAscending
            ? """
              AND (
                    @CursorTimestamp IS NULL
                    OR msg_timestamp_utc > @CursorTimestamp
                    OR (
                        msg_timestamp_utc = @CursorTimestamp
                        AND id > @CursorId
                    )
              )
              """
            : """
              AND (
                    @CursorTimestamp IS NULL
                    OR msg_timestamp_utc < @CursorTimestamp
                    OR (
                        msg_timestamp_utc = @CursorTimestamp
                        AND id < @CursorId
                    )
              )
              """;

        var orderBy = isAscending
            ? "ORDER BY msg_timestamp_utc ASC, id ASC"
            : "ORDER BY msg_timestamp_utc DESC, id DESC";

        var sql = $"""
            SELECT TOP (@Limit)
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
              AND (@From IS NULL OR msg_timestamp_utc >= @From)
              AND (@To IS NULL OR msg_timestamp_utc <= @To)
              AND (@MinLat IS NULL OR latitude >= @MinLat)
              AND (@MaxLat IS NULL OR latitude <= @MaxLat)
              AND (@MinLon IS NULL OR longitude >= @MinLon)
              AND (@MaxLon IS NULL OR longitude <= @MaxLon)
              AND (@NavStatus IS NULL OR nav_status = @NavStatus)
              AND (@MinSog IS NULL OR sog >= @MinSog)
              AND (@MaxSog IS NULL OR sog <= @MaxSog)
              {cursorCondition}
            {orderBy};
            """;

        using var connection = _database.CreateConnection();

        var items = (await connection.QueryAsync<Position>(
            sql,
            new
            {
                Mmsi = mmsi,
                From = from,
                To = to,
                MinLat = minLat,
                MaxLat = maxLat,
                MinLon = minLon,
                MaxLon = maxLon,
                NavStatus = navStatus,
                MinSog = minSog,
                MaxSog = maxSog,
                CursorTimestamp = cursorTimestamp,
                CursorId = cursorId,
                Limit = limit
            })).AsList();

        string? nextCursor = null;

        if (items.Count == limit && items.Count > 0)
        {
            var lastItem = items[^1];

            nextCursor =
                $"{lastItem.MsgTimestampUtc:O}|{lastItem.Id}";
        }

        return new PositionHistoryResult
        {
            Items = items,
            NextCursor = nextCursor
        };
    }
}