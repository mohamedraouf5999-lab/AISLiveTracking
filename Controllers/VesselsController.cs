using AISLiveTracking.API.Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using AISLiveTracking.API.Models;
using AISLiveTracking.API.Data.Services;



namespace AISLiveTracking.API.Controllers;

[ApiController]
[Route("api/vessels")]
public class VesselsController : ControllerBase
{
    private readonly IIdentifierResolver _identifierResolver;
    private readonly ILatestPositionRepository _latestPositionRepository;
    private readonly IVesselRepository _vesselRepository;

    public VesselsController(
        IIdentifierResolver identifierResolver,
        ILatestPositionRepository latestPositionRepository,
        IVesselRepository vesselRepository)
    {
        _identifierResolver = identifierResolver;
        _latestPositionRepository = latestPositionRepository;
        _vesselRepository = vesselRepository;
    }
    [HttpGet("{identifier}/positions/latest")]
    public async Task<IActionResult> GetLatestPosition(
    string identifier,
    [FromQuery] string? idType = null)
    {
        var mmsi = await _identifierResolver.ResolveMmsiAsync(identifier, idType);

        if (mmsi == null)
        {
            return NotFound(new
            {
                message = "Vessel identifier not found."
            });
        }

        var position = await _latestPositionRepository.GetLatestByMmsiAsync(mmsi.Value);

        if (position == null)
        {
            return NotFound(new
            {
                message = "Vessel found but has no stored positions."
            });
        }

        var vessel = await _vesselRepository.GetByMmsiAsync(mmsi.Value);

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
            NavStatusText = NavStatusMapper.GetText(position.NavStatus),
            TimestampUtc = position.MsgTimestampUtc,
            AgeSeconds = (long)(DateTime.UtcNow - position.MsgTimestampUtc).TotalSeconds
        };

        return Ok(response);
    }
}
