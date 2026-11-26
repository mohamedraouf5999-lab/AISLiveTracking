using AISLiveTracking.API.Controllers;
using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace AISLiveTracking.API.Tests;

public class PositionsControllerTests
{
    [Fact]
    public async Task GetPositions_WithoutTimeOrBoundingBox_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetPositions();

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetPositions_WithOnlyFromAndNoBoundingBox_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetPositions(
            from: DateTime.UtcNow.AddDays(-1));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetPositions_WithPartialBoundingBox_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetPositions(minLat: 35);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetPositions_WithInvertedTimeRange_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetPositions(
            from: new DateTime(2026, 9, 1),
            to: new DateTime(2026, 8, 31));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetPositions_WithInvalidCoordinates_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetPositions(
            minLat: 91,
            maxLat: 92,
            minLon: 181,
            maxLon: 182);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetPositions_WithNegativeLimit_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetPositions(
            from: DateTime.UtcNow.AddDays(-1),
            to: DateTime.UtcNow,
            limit: -1);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetPositions_WithInvalidCursor_ReturnsBadRequest()
    {
        var repository = new FakePositionRepository
        {
            ThrowOnCursor = true
        };

        var controller = new PositionsController(repository);

        var result = await controller.GetPositions(
            from: DateTime.UtcNow.AddDays(-1),
            to: DateTime.UtcNow,
            limit: 1,
            cursor: "invalid");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetPositions_WithCombinedFilters_PassesParsedFiltersToRepository()
    {
        var repository = new FakePositionRepository();
        var controller = new PositionsController(repository);

        var result = await controller.GetPositions(
            from: DateTime.UtcNow.AddDays(-1),
            to: DateTime.UtcNow,
            minLat: 2,
            maxLat: 3,
            minLon: 101,
            maxLon: 102,
            mmsi: "525110497,238040000",
            navStatus: "1,8",
            minSog: 0,
            maxSog: 20,
            limit: 10);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(
            new long[] { 525110497, 238040000 },
            repository.LastMmsis);
        Assert.Equal(
            new int[] { 1, 8 },
            repository.LastNavStatusCodes);
        Assert.Equal(10, repository.LastLimit);
    }

    [Fact]
    public async Task GetPositions_WhenLimitExceedsCap_ClampsItTo1000()
    {
        var repository = new FakePositionRepository();
        var controller = new PositionsController(repository);

        var result = await controller.GetPositions(
            from: DateTime.UtcNow.AddDays(-1),
            to: DateTime.UtcNow,
            limit: 5000);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1000, repository.LastLimit);
    }

    private static PositionsController CreateController()
    {
        return new PositionsController(new FakePositionRepository());
    }

    private sealed class FakePositionRepository : IPositionRepository
    {
        public long[]? LastMmsis { get; private set; }

        public int[]? LastNavStatusCodes { get; private set; }

        public int LastLimit { get; private set; }

        public bool ThrowOnCursor { get; init; }

        public Task InsertAsync(
            PositionReport position,
            DateTime messageTime)
        {
            return Task.CompletedTask;
        }

        public Task<Position?> GetLatestByMmsiAsync(long mmsi)
        {
            return Task.FromResult<Position?>(null);
        }

        public Task<PositionHistoryResult> GetPositionsAsync(
            DateTime? from,
            DateTime? to,
            double? minLat,
            double? maxLat,
            double? minLon,
            double? maxLon,
            long[]? mmsis,
            int[]? navStatusCodes,
            double? minSog,
            double? maxSog,
            string sort,
            int limit,
            string? cursor)
        {
            LastMmsis = mmsis;
            LastNavStatusCodes = navStatusCodes;
            LastLimit = limit;

            if (ThrowOnCursor)
            {
                throw new ArgumentException("Invalid cursor.");
            }

            return Task.FromResult(new PositionHistoryResult());
        }
    }
}