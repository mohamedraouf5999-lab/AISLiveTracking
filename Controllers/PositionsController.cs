using AISLiveTracking.API.Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

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
        [FromQuery] int? navStatus = null,
        [FromQuery] double? minSog = null,
        [FromQuery] double? maxSog = null,
        [FromQuery] string sort = "desc",
        [FromQuery] int limit = 100,
        [FromQuery] string? cursor = null)
    {
        if (limit < 1 || limit > 1000)
        {
            return BadRequest(new
            {
                message = "Limit must be between 1 and 1000."
            });
        }

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
            from > to)
        {
            return BadRequest(new
            {
                message = "'from' must be earlier than or equal to 'to'."
            });
        }

        if (!ValidRange(minLat, maxLat, -90, 90) ||
            !ValidRange(minLon, maxLon, -180, 180) ||
            (minSog.HasValue && minSog < 0) ||
            (maxSog.HasValue && maxSog < 0) ||
            (minSog.HasValue &&
             maxSog.HasValue &&
             minSog > maxSog))
        {
            return BadRequest(new
            {
                message = "Invalid coordinate or speed range."
            });
        }

        var result = await _positionRepository.GetPositionsAsync(
            from,
            to,
            minLat,
            maxLat,
            minLon,
            maxLon,
            navStatus,
            minSog,
            maxSog,
            sort,
            limit,
            cursor);

        return Ok(result);
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