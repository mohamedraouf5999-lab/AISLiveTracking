using AISLiveTracking.API.Data.Interfaces;
using System.Globalization;

namespace AISLiveTracking.API.Data.Services;

public class IdentifierResolver : IIdentifierResolver
{
    private readonly IVesselRepository _vesselRepository;

    public IdentifierResolver(IVesselRepository vesselRepository)
    {
        _vesselRepository = vesselRepository;
    }

    public bool IsValidIdentifier(
        string identifier,
        string? idType = null)
    {
        if (string.IsNullOrEmpty(identifier) ||
            !identifier.All(char.IsAsciiDigit))
        {
            return false;
        }

        var normalizedIdType = string.IsNullOrWhiteSpace(idType)
            ? null
            : idType.Trim().ToLowerInvariant();

        return normalizedIdType switch
        {
            "mmsi" => identifier.Length == 9,
            "imo" => identifier.Length == 7,
            null => identifier.Length == 7 || identifier.Length == 9,
            _ => false
        };
    }

    public async Task<long?> ResolveMmsiAsync(
        string identifier,
        string? idType = null)
    {
        if (!IsValidIdentifier(identifier, idType) ||
            !long.TryParse(
                identifier,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return null;
        }

        var normalizedIdType = string.IsNullOrWhiteSpace(idType)
            ? null
            : idType.Trim().ToLowerInvariant();

        if (normalizedIdType == "mmsi" ||
            (normalizedIdType == null && identifier.Length == 9))
        {
            var vessel = await _vesselRepository.GetByMmsiAsync(value);
            return vessel?.Mmsi;
        }

        var vesselByImo =
            await _vesselRepository.GetByImoAsync((int)value);

        return vesselByImo?.Mmsi;
    }
}