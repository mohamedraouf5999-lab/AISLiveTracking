namespace AISLiveTracking.API.Data.Interfaces;

public interface IApiUsageRepository
{
    Task<long> GetMonthlyUsageAsync(
        Guid apiKeyId,
        DateTime fromUtc,
        DateTime toUtc);

    Task RecordAsync(
        Guid apiKeyId,
        string endpoint,
        int statusCode);
}