using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Dapper;

namespace AISLiveTracking.API.Data.Repositories;

public class VesselRepository : IVesselRepository
{
    private readonly DatabaseConnection _database;

    public VesselRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task<Vessel?> GetByMmsiAsync(long mmsi)
    {
        using var connection = _database.CreateConnection();

        const string sql = """
            SELECT *
            FROM vessels
            WHERE mmsi = @Mmsi;
            """;

        return await connection.QueryFirstOrDefaultAsync<Vessel>(
            sql,
            new { Mmsi = mmsi });
    }

    public async Task UpsertAsync(Vessel vessel)
    {
        using var connection = _database.CreateConnection();

        const string sql = """
            MERGE vessels AS target
            USING (SELECT @Mmsi AS mmsi) AS source
            ON target.mmsi = source.mmsi

            WHEN MATCHED THEN
                UPDATE SET
                    imo = @Imo,
                    name = @Name,
                    call_sign = @CallSign,
                    ship_type = @ShipType,
                    last_seen_utc = @LastSeenUtc,
                    updated_utc = SYSUTCDATETIME()

            WHEN NOT MATCHED THEN
                INSERT
                (
                    mmsi,
                    imo,
                    name,
                    call_sign,
                    ship_type,
                    first_seen_utc,
                    last_seen_utc,
                    updated_utc
                )
                VALUES
                (
                    @Mmsi,
                    @Imo,
                    @Name,
                    @CallSign,
                    @ShipType,
                    @FirstSeenUtc,
                    @LastSeenUtc,
                    SYSUTCDATETIME()
                );
            """;

        await connection.ExecuteAsync(sql, vessel);
    }
}