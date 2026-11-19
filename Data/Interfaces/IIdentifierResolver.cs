namespace AISLiveTracking.API.Data.Interfaces;

public interface IIdentifierResolver
{
    bool IsValidIdentifier(string identifier, string? idType = null);

    Task<long?> ResolveMmsiAsync(
        string identifier,
        string? idType = null);
}