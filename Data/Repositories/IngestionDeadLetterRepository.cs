using AISLiveTracking.API.Data.Interfaces;
using Dapper;

namespace AISLiveTracking.API.Data.Repositories;

public class IngestionDeadLetterRepository
    : IIngestionDeadLetterRepository
{
    private readonly DatabaseConnection _database;

    public IngestionDeadLetterRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task SaveAsync(
        string rawPayload,
        string reason,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO dbo.ingestion_dead_letter
            (
                raw_payload,
                reason
            )
            VALUES
            (
                @RawPayload,
                @Reason
            );
            """;

        using var connection = _database.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    RawPayload = rawPayload,
                    Reason = reason
                },
                cancellationToken: cancellationToken));
    }
}