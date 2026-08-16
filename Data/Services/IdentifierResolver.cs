using AISLiveTracking.API.Data.Interfaces;

namespace AISLiveTracking.API.Data.Services;

public class IdentifierResolver : IIdentifierResolver
{
    private readonly IVesselRepository _vesselRepository;

    public IdentifierResolver(IVesselRepository vesselRepository)
    {
        _vesselRepository = vesselRepository;
    }

   public async Task<long?> ResolveMmsiAsync(string identifier, string? idType = null)
{
    if (string.IsNullOrWhiteSpace(identifier))
    {
        return null;
    }

    identifier = identifier.Trim();

    if (!long.TryParse(identifier, out var value))
    {
        return null;
    }

    if (!string.IsNullOrWhiteSpace(idType))
    {
        idType = idType.Trim().ToLowerInvariant();

        if (idType == "mmsi")
        {
            var vessel = await _vesselRepository.GetByMmsiAsync(value);

            return vessel?.Mmsi;
        }

        if (idType == "imo")
        {
            var vessel = await _vesselRepository.GetByImoAsync((int)value);

            return vessel?.Mmsi;
        }

        return null;
    }

    if (identifier.Length == 9)
    {
        var vessel = await _vesselRepository.GetByMmsiAsync(value);

        return vessel?.Mmsi;
    }

    if (identifier.Length == 7)
    {
        var vessel = await _vesselRepository.GetByImoAsync((int)value);

        return vessel?.Mmsi;
    }

    return null;
}
}