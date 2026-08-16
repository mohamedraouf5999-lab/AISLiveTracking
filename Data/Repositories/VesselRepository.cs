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
    public async Task<Vessel?> GetByImoAsync(int imo)
    {
        using var connection = _database.CreateConnection();

        const string sql = """
        SELECT *
        FROM vessels
        WHERE imo = @Imo;
        """;

        return await connection.QueryFirstOrDefaultAsync<Vessel>(
            sql,
            new { Imo = imo });
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
                imo = COALESCE(@Imo, target.imo),
                name = COALESCE(@VesselName, target.name),
                call_sign = COALESCE(@CallSign, target.call_sign),
                ship_type = COALESCE(@ShipType, target.ship_type),
                dim_to_bow = COALESCE(@DimToBow, target.dim_to_bow),
                dim_to_stern = COALESCE(@DimToStern, target.dim_to_stern),
                dim_to_port = COALESCE(@DimToPort, target.dim_to_port),
                dim_to_starboard = COALESCE(@DimToStarboard, target.dim_to_starboard),
                draught = COALESCE(@Draught, target.draught),
                destination = COALESCE(@Destination, target.destination),
                eta = COALESCE(@Eta, target.eta),
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
                dim_to_bow,
                dim_to_stern,
                dim_to_port,
                dim_to_starboard,
                draught,
                destination,
                eta,
                first_seen_utc,
                last_seen_utc,
                updated_utc
            )
            VALUES
            (
                @Mmsi,
                @Imo,
                @VesselName,
                @CallSign,
                @ShipType,
                @DimToBow,
                @DimToStern,
                @DimToPort,
                @DimToStarboard,
                @Draught,
                @Destination,
                @Eta,
                @FirstSeenUtc,
                @LastSeenUtc,
                SYSUTCDATETIME()
            );
        """;

    await connection.ExecuteAsync(sql, vessel);
}
}