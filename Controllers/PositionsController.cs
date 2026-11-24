using AISLiveTracking.API.Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;

namespace AISLiveTracking.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Operator,ReadOnly")]
[EnableRateLimiting("ApiKeyRateLimit")]
[Route("api/positions")]
public class PositionsController : ControllerBase
{
    private readonly IPositionRepository _positionRepository;

    public PositionsController(IPositionRepository positionRepository)
    {
        _positionRepository = positionRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetPositions(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] double? minLat = null,
        [FromQuery] double? maxLat = null,
        [FromQuery] double? minLon = null,
        [FromQuery] double? maxLon = null,
        [FromQuery] string? mmsi = null,
        [FromQuery] string? navStatus = null,
        [FromQuery] double? minSog = null,
        [FromQuery] double? maxSog = null,
        [FromQuery] string sort = "desc",
        [FromQuery] int limit = 100,
        [FromQuery] string? cursor = null)
    {
        if (limit < 0)
        {
            return BadRequest(new
            {
                message = "Limit cannot be negative."
            });
        }

        limit = Math.Min(limit, 1000);

        if (!string.Equals(
                sort,
                "asc",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                sort,
                "desc",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Sort must be either 'asc' or 'desc'."
            });
        }

        if (from.HasValue &&
            to.HasValue &&
            from.Value > to.Value)
        {
            return BadRequest(new
            {
                message = "'from' must be earlier than or equal to 'to'."
            });
        }

        var boundingBoxValuesCount =
            (minLat.HasValue ? 1 : 0) +
            (maxLat.HasValue ? 1 : 0) +
            (minLon.HasValue ? 1 : 0) +
            (maxLon.HasValue ? 1 : 0);

        if (boundingBoxValuesCount != 0 &&
            boundingBoxValuesCount != 4)
        {
            return BadRequest(new
            {
                message = "Provide all four bounding box values or none."
            });
        }

        var hasBoundingBox = boundingBoxValuesCount == 4;
        var hasTimeFilter = from.HasValue || to.HasValue;

        if (!hasBoundingBox && !hasTimeFilter)
        {
            return BadRequest(new
            {
                message = "Provide a time filter or a complete bounding box."
            });
        }

        if (!ValidRange(minLat, maxLat, -90, 90) ||
            !ValidRange(minLon, maxLon, -180, 180) ||
            (minSog.HasValue && minSog.Value < 0) ||
            (maxSog.HasValue && maxSog.Value < 0) ||
            (minSog.HasValue &&
             maxSog.HasValue &&
             minSog.Value > maxSog.Value))
        {
            return BadRequest(new
            {
                message = "Invalid coordinate or speed range."
            });
        }

        if (!TryParseMmsiList(mmsi, out var mmsiValues))
        {
            return BadRequest(new
            {
                message = "mmsi must be a comma-separated list of 9-digit MMSIs."
            });
        }

        if (!TryParseNavStatusCodes(navStatus, out var navStatusValues))
        {
            return BadRequest(new
            {
                message = "navStatus must contain codes from 0 to 15, separated by commas."
            });
        }

        try
        {
            var result = await _positionRepository.GetPositionsAsync(
                from,
                to,
                minLat,
                maxLat,
                minLon,
                maxLon,
                mmsiValues,
                navStatusValues,
                minSog,
                maxSog,
                sort,
                limit,
                cursor);

            return Ok(result);
        }
        catch (ArgumentException)
        {
            return BadRequest(new
            {
                message = "Invalid cursor."
            });
        }
    }

    private static bool TryParseMmsiList(
        string? value,
        out long[]? mmsiValues)
    {
        mmsiValues = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var entries = value.Split(
            ',',
            StringSplitOptions.TrimEntries);

        var parsedValues = new List<long>();

        foreach (var entry in entries)
        {
            if (entry.Length != 9 ||
                !entry.All(char.IsAsciiDigit) ||
                !long.TryParse(
                    entry,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsedMmsi) ||
                parsedMmsi < 100_000_000 ||
                parsedMmsi > 999_999_999)
            {
                return false;
            }

            parsedValues.Add(parsedMmsi);
        }

        mmsiValues = parsedValues
            .Distinct()
            .ToArray();

        return true;
    }

    private static bool TryParseNavStatusCodes(
        string? value,
        out int[]? navStatusValues)
    {
        navStatusValues = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var entries = value.Split(
            ',',
            StringSplitOptions.TrimEntries);

        var parsedValues = new List<int>();

        foreach (var entry in entries)
        {
            if (!int.TryParse(
                    entry,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsedStatus) ||
                parsedStatus < 0 ||
                parsedStatus > 15)
            {
                return false;
            }

            parsedValues.Add(parsedStatus);
        }

        navStatusValues = parsedValues
            .Distinct()
            .ToArray();

        return true;
    }

    private static bool ValidRange(
        double? min,
        double? max,
        double lower,
        double upper) =>
        (!min.HasValue ||
         min.Value >= lower && min.Value <= upper) &&
        (!max.HasValue ||
         max.Value >= lower && max.Value <= upper) &&
        (!min.HasValue ||
         !max.HasValue ||
         min.Value <= max.Value);
}