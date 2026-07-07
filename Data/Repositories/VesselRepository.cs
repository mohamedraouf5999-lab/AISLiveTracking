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
            FROM Vessels
            WHERE MMSI = @MMSI;
            """;

        return await connection.QueryFirstOrDefaultAsync<Vessel>(
            sql,
            new { MMSI = mmsi });
    }
}