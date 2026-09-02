using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Data.Services;
using AISLiveTracking.API.Models;
using AISLiveTracking.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AISLiveTracking.API.Controllers;

[ApiController]
[Route("api/vessels")]
public class VesselsController : ControllerBase
{
    private readonly IIdentifierResolver _identifierResolver;
    private readonly ILatestPositionRepository _latestPositionRepository;
    private readonly IPositionHistoryRepository _positionHistoryRepository;
    private readonly IVesselRepository _vesselRepository;
    private readonly IVesselAnalyticsService _vesselAnalyticsService;

    public VesselsController(
        IIdentifierResolver identifierResolver,
        ILatestPositionRepository latestPositionRepository,
        IPositionHistoryRepository positionHistoryRepository,
        IVesselRepository vesselRepository,
        IVesselAnalyticsService vesselAnalyticsService)
    {
        _identifierResolver = identifierResolver;
        _latestPositionRepository = latestPositionRepository;
        _positionHistoryRepository = positionHistoryRepository;
        _vesselRepository = vesselRepository;
        _vesselAnalyticsService = vesselAnalyticsService;
    }

    [HttpGet("{identifier}/positions/history")]
    public async Task<IActionResult> GetPositionHistory(
        string identifier,
        [FromQuery] string? idType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] double? minLat = null,
        [FromQuery] double? maxLat = null,
        [FromQuery] double? minLon = null,
        [FromQuery] double? maxLon = null,
        [FromQuery] string? navStatus = null,
        [FromQuery] double? minSog = null,
        [FromQuery] double? maxSog = null,
        [FromQuery] string sort = "desc",
        [FromQuery] int limit = 100,
        [FromQuery] string? cursor = null)
    {
        var mmsi = await _identifierResolver.ResolveMmsiAsync(
            identifier,
            idType);

        if (mmsi == null)
        {
            return NotFound(new
            {
                message = "Vessel identifier not found."
            });
        }

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

        var result = await _positionHistoryRepository.GetHistoryAsync(
            mmsi.Value,
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

    [HttpGet("{identifier}/positions/latest")]
    public async Task<IActionResult> GetLatestPosition(
        string identifier,
        [FromQuery] string? idType = null)
    {
        var mmsi = await _identifierResolver.ResolveMmsiAsync(
            identifier,
            idType);

        if (mmsi == null)
        {
            return NotFound(new
            {
                message = "Vessel identifier not found."
            });
        }

        var position =
            await _latestPositionRepository.GetLatestByMmsiAsync(
                mmsi.Value);

        if (position == null)
        {
            return NotFound(new
            {
                message = "Vessel found but has no stored positions."
            });
        }

        var vessel =
            await _vesselRepository.GetByMmsiAsync(mmsi.Value);

        var response = new LatestPositionResponse
        {
            Mmsi = position.Mmsi,
            Imo = vessel?.Imo,
            Name = vessel?.VesselName,
            Latitude = position.Latitude,
            Longitude = position.Longitude,
            Sog = position.Sog,
            Cog = position.Cog,
            NavStatus = position.NavStatus,
            NavStatusText =
                NavStatusMapper.GetText(position.NavStatus),
            TimestampUtc = position.MsgTimestampUtc,
            AgeSeconds =
                (long)(
                    DateTime.UtcNow -
                    position.MsgTimestampUtc).TotalSeconds
        };

        return Ok(response);
    }

    [HttpGet("{identifier}/analytics")]
    public async Task<IActionResult> GetAnalytics(
        string identifier,
        [FromQuery] string? idType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        if (!from.HasValue || !to.HasValue)
        {
            return BadRequest(new
            {
                message = "'from' and 'to' are required."
            });
        }

        if (from > to)
        {
            return BadRequest(new
            {
                message =
                    "'from' must be earlier than or equal to 'to'."
            });
        }

        var mmsi =
            await _identifierResolver.ResolveMmsiAsync(
                identifier,
                idType);

        if (mmsi == null)
        {
            return NotFound(new
            {
                message = "Vessel identifier not found."
            });
        }

        var result =
            await _vesselAnalyticsService.GetAnalyticsAsync(
                mmsi.Value,
                from.Value,
                to.Value);

        return Ok(result);
    }
}