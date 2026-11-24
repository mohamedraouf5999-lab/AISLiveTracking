using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Data.Services;
using AISLiveTracking.API.Models;
using AISLiveTracking.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AISLiveTracking.API.Controllers;

[Authorize(Roles = "Admin,Operator,ReadOnly")]
[EnableRateLimiting("ApiKeyRateLimit")]
[ApiController]
[Route("api/vessels")]
public class VesselsController : ControllerBase
{
    private readonly IIdentifierResolver _identifierResolver;
    private readonly ILatestPositionRepository _latestPositionRepository;
    private readonly IPositionHistoryRepository _positionHistoryRepository;
    private readonly IVesselRepository _vesselRepository;
    private readonly IVesselAnalyticsService _vesselAnalyticsService;
    private readonly IPlanRepository _planRepository;

    public VesselsController(
        IIdentifierResolver identifierResolver,
        ILatestPositionRepository latestPositionRepository,
        IPositionHistoryRepository positionHistoryRepository,
        IVesselRepository vesselRepository,
        IVesselAnalyticsService vesselAnalyticsService,
        IPlanRepository planRepository)
    {
        _identifierResolver = identifierResolver;
        _latestPositionRepository = latestPositionRepository;
        _positionHistoryRepository = positionHistoryRepository;
        _vesselRepository = vesselRepository;
        _vesselAnalyticsService = vesselAnalyticsService;
        _planRepository = planRepository;
    }

    [HttpGet("{identifier}")]
    public async Task<IActionResult> GetVessel(
        string identifier,
        [FromQuery] string? idType = null)
    {
        if (!_identifierResolver.IsValidIdentifier(identifier, idType))
        {
            return BadRequest(new
            {
                message = "Identifier must be a 7-digit IMO or 9-digit MMSI, and idType must match."
            });
        }

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

        var vessel = await _vesselRepository.GetByMmsiAsync(mmsi.Value);

        if (vessel == null)
        {
            return NotFound(new
            {
                message = "Vessel identifier not found."
            });
        }

        return Ok(new
        {
            mmsi = vessel.Mmsi,
            imo = vessel.Imo,
            name = vessel.VesselName?.Trim(),
            callSign = vessel.CallSign?.Trim(),
            shipType = vessel.ShipType,
            dimToBow = vessel.DimToBow,
            dimToStern = vessel.DimToStern,
            dimToPort = vessel.DimToPort,
            dimToStarboard = vessel.DimToStarboard,
            draught = vessel.Draught,
            destination = vessel.Destination?.Trim(),
            eta = vessel.Eta.HasValue
                ? DateTime.SpecifyKind(
                    vessel.Eta.Value,
                    DateTimeKind.Utc)
                : (DateTime?)null,
            firstSeenUtc = DateTime.SpecifyKind(
                vessel.FirstSeenUtc,
                DateTimeKind.Utc),
            lastSeenUtc = DateTime.SpecifyKind(
                vessel.LastSeenUtc,
                DateTimeKind.Utc)
        });
    }

    [HttpGet("{identifier}/positions")]
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

        if (boundingBoxValuesCount == 4 &&
            (minLat < -90 ||
             maxLat > 90 ||
             minLon < -180 ||
             maxLon > 180 ||
             minLat > maxLat ||
             minLon > maxLon))
        {
            return BadRequest(new
            {
                message = "Bounding box coordinates are invalid."
            });
        }

        if (!string.IsNullOrWhiteSpace(navStatus))
        {
            var navStatusCodes = navStatus.Split(
                ',',
                StringSplitOptions.TrimEntries);

            if (navStatusCodes.Any(code =>
                    !byte.TryParse(code, out var parsedStatus) ||
                    parsedStatus > 15))
            {
                return BadRequest(new
                {
                    message = "navStatus must contain codes from 0 to 15, separated by commas."
                });
            }
        }

        var planId = User.FindFirst("plan_id")?.Value;

        if (!int.TryParse(planId, out var parsedPlanId))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "Plan information is missing."
                });
        }

        var plan = await _planRepository.GetByIdAsync(parsedPlanId);

        if (plan == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "API plan not found."
                });
        }

        if (plan.MaxHistoryDays.HasValue)
        {
            var minimumAllowedFrom =
                DateTime.UtcNow.AddDays(-plan.MaxHistoryDays.Value);

            if (!from.HasValue || from.Value < minimumAllowedFrom)
            {
                from = minimumAllowedFrom;
            }

            if (!to.HasValue || to.Value > DateTime.UtcNow)
            {
                to = DateTime.UtcNow;
            }
        }

        if (from.HasValue &&
            to.HasValue &&
            from.Value > to.Value)
        {
            return BadRequest(new
            {
                message =
                    "Requested range is outside the history allowed by your plan."
            });
        }

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

        try
        {
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
        catch (ArgumentException)
        {
            return BadRequest(new
            {
                message = "Invalid cursor."
            });
        }
    }

    [HttpGet("{identifier}/positions/latest")]
    public async Task<IActionResult> GetLatestPosition(
        string identifier,
        [FromQuery] string? idType = null)
    {
        if (!_identifierResolver.IsValidIdentifier(identifier, idType))
        {
            return BadRequest(new
            {
                message = "Identifier must be a 7-digit IMO or 9-digit MMSI, and idType must match."
            });
        }

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
            Name = vessel?.VesselName?.Trim(),
            Latitude = position.Latitude,
            Longitude = position.Longitude,
            Sog = position.Sog,
            Cog = position.Cog,
            NavStatus = position.NavStatus,
            NavStatusText =
                NavStatusMapper.GetText(position.NavStatus),
            TimestampUtc = DateTime.SpecifyKind(
                position.MsgTimestampUtc,
                DateTimeKind.Utc),
            AgeSeconds =
                (long)(
                    DateTime.UtcNow -
                    DateTime.SpecifyKind(
                        position.MsgTimestampUtc,
                        DateTimeKind.Utc)).TotalSeconds
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

        if (from.Value > to.Value)
        {
            return BadRequest(new
            {
                message =
                    "'from' must be earlier than or equal to 'to'."
            });
        }

        if ((to.Value - from.Value) > TimeSpan.FromDays(31))
        {
            return BadRequest(new
            {
                message = "Analytics range cannot exceed 31 days."
            });
        }

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

        var analyticsEnabled =
            User.FindFirst("analytics_enabled")?.Value;

        if (!bool.TryParse(
                analyticsEnabled,
                out var planAnalyticsEnabled) ||
            !planAnalyticsEnabled)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "Analytics is not available for your plan."
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