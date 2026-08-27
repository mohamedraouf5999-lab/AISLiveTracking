namespace AISLiveTracking.API.Data.Interfaces;

public interface IIdentifierResolver
{
    Task<long?> ResolveMmsiAsync(string identifier, string? idType = null);
}