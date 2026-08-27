using AISLiveTracking.API.Data.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AISLiveTracking.API.Controllers;

[ApiController]
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

        if (!string.Equals(sort, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sort, "desc", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Sort must be either 'asc' or 'desc'."
            });
        }

        if (from.HasValue && to.HasValue && from > to)
        {
            return BadRequest(new
            {
                message = "'from' must be earlier than or equal to 'to'."
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
}