using AISLiveTracking.API.Controllers;
using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Data.Services;
using AISLiveTracking.API.Models;
using AISLiveTracking.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AISLiveTracking.API.Tests;

public class VesselsControllerTests
{
    [Fact]
    public async Task GetLatestPosition_WhenVesselIsUnknown_ReturnsNotFoundMessage()
    {
        var controller = CreateController();

        var result = await controller.GetLatestPosition("123456789");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        Assert.Equal(
            "Vessel identifier not found.",
            GetMessage(notFound.Value));
    }

    [Fact]
    public async Task GetLatestPosition_WhenVesselHasNoPositions_ReturnsDistinctNotFoundMessage()
    {
        var controller = CreateController(
            new Vessel { Mmsi = 123456789 });

        var result = await controller.GetLatestPosition("123456789");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        Assert.Equal(
            "Vessel found but has no stored positions.",
            GetMessage(notFound.Value));
    }

    [Fact]
    public async Task GetLatestPosition_WhenIdTypeConflictsWithIdentifier_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetLatestPosition(
            "123456789",
            "imo");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static VesselsController CreateController(
        params Vessel[] vessels)
    {
        var vesselRepository = new InMemoryVesselRepository(vessels);

        return new VesselsController(
            new IdentifierResolver(vesselRepository),
            new EmptyLatestPositionRepository(),
            new EmptyPositionHistoryRepository(),
            vesselRepository,
            new EmptyVesselAnalyticsService(),
            new EmptyPlanRepository());
    }

    private static string? GetMessage(object? value)
    {
        return value?
            .GetType()
            .GetProperty("message")?
            .GetValue(value)?
            .ToString();
    }

    private sealed class InMemoryVesselRepository : IVesselRepository
    {
        private readonly List<Vessel> _vessels;

        public InMemoryVesselRepository(params Vessel[] vessels)
        {
            _vessels = vessels.ToList();
        }

        public Task<Vessel?> GetByMmsiAsync(long mmsi)
        {
            return Task.FromResult(
                _vessels.FirstOrDefault(vessel => vessel.Mmsi == mmsi));
        }

        public Task<Vessel?> GetByImoAsync(int imo)
        {
            return Task.FromResult(
                _vessels.FirstOrDefault(vessel => vessel.Imo == imo));
        }

        public Task UpsertAsync(Vessel vessel)
        {
            _vessels.Add(vessel);
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyLatestPositionRepository
        : ILatestPositionRepository
    {
        public Task<Position?> GetLatestByMmsiAsync(long mmsi)
        {
            return Task.FromResult<Position?>(null);
        }
    }

    private sealed class EmptyPositionHistoryRepository
        : IPositionHistoryRepository
    {
        public Task<PositionHistoryResult> GetHistoryAsync(
            long mmsi,
            DateTime? from,
            DateTime? to,
            double? minLat,
            double? maxLat,
            double? minLon,
            double? maxLon,
            string? navStatus,
            double? minSog,
            double? maxSog,
            string sort,
            int limit,
            string? cursor)
        {
            return Task.FromResult(new PositionHistoryResult());
        }
    }

    private sealed class EmptyVesselAnalyticsService
        : IVesselAnalyticsService
    {
        public Task<VesselAnalyticsResult> GetAnalyticsAsync(
            long mmsi,
            DateTime from,
            DateTime to)
        {
            return Task.FromResult(new VesselAnalyticsResult());
        }
    }

    private sealed class EmptyPlanRepository : IPlanRepository
    {
        public Task<Plan?> GetByIdAsync(int id)
        {
            return Task.FromResult<Plan?>(null);
        }
    }
}