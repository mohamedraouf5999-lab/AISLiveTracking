using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Dapper;
using System.Data;
using System.Globalization;

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

        var navStatusCodes = string.IsNullOrWhiteSpace(navStatus)
            ? Array.Empty<int>()
            : navStatus
                .Split(',', StringSplitOptions.TrimEntries)
                .Select(code => int.Parse(
                    code,
                    CultureInfo.InvariantCulture))
                .Distinct()
                .ToArray();

        var navStatusFilter = navStatusCodes.Length > 0
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
            WHERE mmsi = @Mmsi
              AND (@From IS NULL OR msg_timestamp_utc >= @From)
              AND (@To IS NULL OR msg_timestamp_utc < @To)
              AND (@MinLat IS NULL OR latitude >= @MinLat)
              AND (@MaxLat IS NULL OR latitude <= @MaxLat)
              AND (@MinLon IS NULL OR longitude >= @MinLon)
              AND (@MaxLon IS NULL OR longitude <= @MaxLon)
              AND (@MinSog IS NULL OR sog >= @MinSog)
              AND (@MaxSog IS NULL OR sog <= @MaxSog)
              {navStatusFilter}
              {cursorCondition}
            {orderBy};
            """;

        using var connection = _database.CreateConnection();

        var parameters = new DynamicParameters();

        parameters.Add("Mmsi", mmsi, DbType.Int64);
        parameters.Add("From", from, DbType.DateTime2);
        parameters.Add("To", to, DbType.DateTime2);
        parameters.Add("MinLat", minLat);
        parameters.Add("MaxLat", maxLat);
        parameters.Add("MinLon", minLon);
        parameters.Add("MaxLon", maxLon);
        parameters.Add("MinSog", minSog);
        parameters.Add("MaxSog", maxSog);
        parameters.Add("NavStatusCodes", navStatusCodes);
        parameters.Add(
            "CursorTimestamp",
            cursorTimestamp,
            DbType.DateTime2);
        parameters.Add("CursorId", cursorId, DbType.Int64);
        parameters.Add("FetchLimit", limit + 1);

        var items = (await connection.QueryAsync<Position>(
            sql,
            parameters)).AsList();

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