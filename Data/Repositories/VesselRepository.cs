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
        SELECT
            mmsi AS Mmsi,
            imo AS Imo,
            name AS VesselName,
            call_sign AS CallSign,
            ship_type AS ShipType,
            dim_to_bow AS DimToBow,
            dim_to_stern AS DimToStern,
            dim_to_port AS DimToPort,
            dim_to_starboard AS DimToStarboard,
            draught AS Draught,
            destination AS Destination,
            eta AS Eta,
            first_seen_utc AS FirstSeenUtc,
            last_seen_utc AS LastSeenUtc,
            up dated_utc AS UpdatedUtc
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
        SELECT
            mmsi AS Mmsi,
            imo AS Imo,
            name AS VesselName,
            call_sign AS CallSign,
            ship_type AS ShipType,
            dim_to_bow AS DimToBow,
            dim_to_stern AS DimToStern,
            dim_to_port AS DimToPort,
            dim_to_starboard AS DimToStarboard,
            draught AS Draught,
            destination AS Destination,
            eta AS Eta,
            first_seen_utc AS FirstSeenUtc,
            last_seen_utc AS LastSeenUtc,
            updated_utc AS UpdatedUtc
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
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;

        DECLARE @EffectiveImo int = @Imo;

        IF @EffectiveImo IS NOT NULL
           AND EXISTS
           (
               SELECT 1
               FROM dbo.vessels WITH (UPDLOCK, HOLDLOCK)
               WHERE imo = @EffectiveImo
                 AND mmsi <> @Mmsi
           )
        BEGIN
            SET @EffectiveImo = NULL;
        END;

        IF EXISTS
        (
            SELECT 1
            FROM dbo.vessels WITH (UPDLOCK, HOLDLOCK)
            WHERE mmsi = @Mmsi
        )
        BEGIN
            UPDATE dbo.vessels
            SET
                imo = COALESCE(@EffectiveImo, imo),
                name = COALESCE(@VesselName, name),
                call_sign = COALESCE(@CallSign, call_sign),
                ship_type = COALESCE(@ShipType, ship_type),
                dim_to_bow = COALESCE(@DimToBow, dim_to_bow),
                dim_to_stern = COALESCE(@DimToStern, dim_to_stern),
                dim_to_port = COALESCE(@DimToPort, dim_to_port),
                dim_to_starboard = COALESCE(@DimToStarboard, dim_to_starboard),
                draught = COALESCE(@Draught, draught),
                destination = COALESCE(@Destination, destination),
                eta = COALESCE(@Eta, eta),
                last_seen_utc = @LastSeenUtc,
                updated_utc = SYSUTCDATETIME()
            WHERE mmsi = @Mmsi;
        END
        ELSE
        BEGIN
            INSERT INTO dbo.vessels
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
                @EffectiveImo,
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
        END;

        COMMIT TRANSACTION;
        """;

    await connection.ExecuteAsync(sql, vessel);
}
}