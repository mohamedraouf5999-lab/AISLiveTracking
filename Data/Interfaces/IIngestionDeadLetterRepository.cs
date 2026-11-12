namespace AISLiveTracking.API.Data.Interfaces;

public interface IIngestionDeadLetterRepository
{
    Task SaveAsync(
        string rawPayload,
        string reason,
        CancellationToken cancellationToken = default);
}