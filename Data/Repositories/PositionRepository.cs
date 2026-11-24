using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Dapper;
using System.Globalization;

namespace AISLiveTracking.API.Data.Repositories;

public class PositionRepository : IPositionRepository
{
    private readonly DatabaseConnection _database;

    public PositionRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task InsertAsync(
        PositionReport position,
        DateTime messageTime)
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
            WHERE mmsi = @Mmsi
            ORDER BY msg_timestamp_utc DESC, id DESC;
            """;

        return await connection.QueryFirstOrDefaultAsync<Position>(
            sql,
            new { Mmsi = mmsi });
    }

    public async Task<PositionHistoryResult> GetPositionsAsync(
        DateTime? from,
        DateTime? to,
        double? minLat,
        double? maxLat,
        double? minLon,
        double? maxLon,
        long[]? mmsis,
        int[]? navStatusCodes,
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
        long? cursorId = null;

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var parts = cursor.Split('|', 2);

            if (parts.Length != 2 ||
                !DateTime.TryParse(
                    parts[0],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsedTimestamp) ||
                !long.TryParse(
                    parts[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsedId))
            {
                throw new ArgumentException("Invalid cursor.");
            }

            cursorTimestamp = parsedTimestamp;
            cursorId = parsedId;
        }

        var mmsiFilter = mmsis is { Length: > 0 }
            ? "AND mmsi IN @Mmsis"
            : string.Empty;

        var navStatusFilter = navStatusCodes is { Length: > 0 }
            ? "AND nav_status IN @NavStatusCodes"
            : string.Empty;

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
            SELECT TOP (@FetchLimit)
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
            WHERE (@From IS NULL OR msg_timestamp_utc >= @From)
              AND (@To IS NULL OR msg_timestamp_utc < @To)
              AND (@MinLat IS NULL OR latitude >= @MinLat)
              AND (@MaxLat IS NULL OR latitude <= @MaxLat)
              AND (@MinLon IS NULL OR longitude >= @MinLon)
              AND (@MaxLon IS NULL OR longitude <= @MaxLon)
              AND (@MinSog IS NULL OR sog >= @MinSog)
              AND (@MaxSog IS NULL OR sog <= @MaxSog)
              {mmsiFilter}
              {navStatusFilter}
              {cursorCondition}
            {orderBy};
            """;

        using var connection = _database.CreateConnection();

        var items = (await connection.QueryAsync<Position>(
            sql,
            new
            {
                From = from,
                To = to,
                MinLat = minLat,
                MaxLat = maxLat,
                MinLon = minLon,
                MaxLon = maxLon,
                Mmsis = mmsis,
                NavStatusCodes = navStatusCodes,
                MinSog = minSog,
                MaxSog = maxSog,
                CursorTimestamp = cursorTimestamp,
                CursorId = cursorId,
                FetchLimit = limit + 1
            })).AsList();

        var hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        string? nextCursor = null;

        if (hasMore && items.Count > 0)
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