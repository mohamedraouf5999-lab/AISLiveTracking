using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using AISLiveTracking.API.Services;
using Microsoft.Extensions.Options;

namespace AISLiveTracking.API.Tests;

public class VesselAnalyticsServiceTests
{
    private static readonly DateTime WindowStart =
        new(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAnalyticsAsync_WithNoPositions_ReturnsZeroedResult()
    {
        var service = CreateService(Array.Empty<Position>());

        var result = await service.GetAnalyticsAsync(
            413489920,
            WindowStart,
            WindowStart.AddHours(1));

        Assert.Equal(413489920, result.Mmsi);
        Assert.Equal(0, result.DistanceKm);
        Assert.Equal(0, result.DistanceExcludedKm);
        Assert.Equal(0, result.Reporting.Messages);
        Assert.Equal(0, result.Window.CoveragePercent);
        Assert.Null(result.Speed.AvgSogKnots);
    }

    [Fact]
    public async Task GetAnalyticsAsync_WithOnePosition_DoesNotDivideByZero()
    {
        var positions = new[]
        {
            CreatePosition(
                WindowStart,
                latitude: 0,
                longitude: 0,
                sog: 10,
                navStatus: 0)
        };

        var service = CreateService(positions);

        var result = await service.GetAnalyticsAsync(
            413489920,
            WindowStart,
            WindowStart.AddHours(1));

        Assert.Equal(1, result.Reporting.Messages);
        Assert.Equal(WindowStart, result.Reporting.First);
        Assert.Equal(WindowStart, result.Reporting.Last);
        Assert.Equal(0, result.DistanceKm);
        Assert.Equal(0, result.Window.CoveragePercent);
        Assert.Null(result.Reporting.MedianIntervalSeconds);
    }

    [Fact]
    public async Task GetAnalyticsAsync_WithNormalTrack_ComputesCoreMetrics()
    {
        var positions = new[]
        {
            CreatePosition(
                WindowStart,
                latitude: 0,
                longitude: 0,
                sog: 10,
                navStatus: 0),
            CreatePosition(
                WindowStart.AddMinutes(30),
                latitude: 0,
                longitude: 0.01,
                sog: 12,
                navStatus: 0),
            CreatePosition(
                WindowStart.AddHours(1),
                latitude: 0,
                longitude: 0.02,
                sog: 14,
                navStatus: 1)
        };

        var service = CreateService(positions);

        var result = await service.GetAnalyticsAsync(
            413489920,
            WindowStart,
            WindowStart.AddHours(2));

        Assert.InRange(result.DistanceKm, 2.22, 2.23);
        Assert.Equal(0, result.DistanceExcludedKm);
        Assert.Equal(12, result.Speed.AvgSogKnots);
        Assert.Equal(14, result.Speed.MaxSogKnots);
        Assert.InRange(
            result.Speed.MaxImpliedKnots!.Value,
            1.19,
            1.21);
        Assert.Equal(1, result.Time.UnderwayHours);
        Assert.Equal(0, result.Time.StoppedHours);
        Assert.Equal(1, result.Time.ByNavStatus[0]);
        Assert.Equal(50, result.Window.CoveragePercent);
        Assert.Equal(3, result.Reporting.Messages);
        Assert.Equal(1800, result.Reporting.MedianIntervalSeconds);
    }

    [Fact]
    public async Task GetAnalyticsAsync_WithLongGap_ExcludesAndReportsDistance()
    {
        var positions = new[]
        {
            CreatePosition(
                WindowStart,
                latitude: 0,
                longitude: 0,
                sog: 10,
                navStatus: 0),
            CreatePosition(
                WindowStart.AddHours(2),
                latitude: 0,
                longitude: 0.01,
                sog: 10,
                navStatus: 0)
        };

        var service = CreateService(positions);

        var result = await service.GetAnalyticsAsync(
            413489920,
            WindowStart,
            WindowStart.AddHours(3));

        Assert.Equal(0, result.DistanceKm);
        Assert.InRange(result.DistanceExcludedKm, 1.10, 1.12);
        Assert.Equal(1, result.Gaps.Count);
        Assert.Equal(2, result.Gaps.TotalDarkHours);
        Assert.NotNull(result.Gaps.Longest);
        Assert.Equal(2, result.Gaps.Longest!.Hours);
        Assert.Equal(WindowStart, result.Gaps.Longest.From);
        Assert.Equal(WindowStart.AddHours(2), result.Gaps.Longest.To);
    }

    [Fact]
    public async Task GetAnalyticsAsync_WithAllStoppedTrack_ComputesStoppedAndIdleTime()
    {
        var positions = new[]
        {
            CreatePosition(
                WindowStart,
                latitude: 0,
                longitude: 0,
                sog: 0,
                navStatus: 5),
            CreatePosition(
                WindowStart.AddMinutes(30),
                latitude: 0,
                longitude: 0,
                sog: 0,
                navStatus: 5),
            CreatePosition(
                WindowStart.AddHours(1),
                latitude: 0,
                longitude: 0,
                sog: 0,
                navStatus: 5)
        };

        var service = CreateService(positions);

        var result = await service.GetAnalyticsAsync(
            413489920,
            WindowStart,
            WindowStart.AddHours(1));

        Assert.Equal(0, result.Time.UnderwayHours);
        Assert.Equal(1, result.Time.StoppedHours);
        Assert.Equal(1, result.IdleHours);
        Assert.Equal(1, result.Time.ByNavStatus[5]);
        Assert.Equal(3, result.Reporting.Messages);
    }

    private static VesselAnalyticsService CreateService(
        IReadOnlyList<Position> positions)
    {
        var repository = new FakeAnalyticsRepository(positions);

        var options = Options.Create(new AnalyticsOptions
        {
            GapThresholdMinutes = 60,
            StopSpeedKnots = 0.5m,
            IdleMinimumMinutes = 30
        });

        return new VesselAnalyticsService(repository, options);
    }

    private static Position CreatePosition(
        DateTime timestamp,
        double latitude,
        double longitude,
        decimal sog,
        int navStatus)
    {
        return new Position
        {
            MsgTimestampUtc = timestamp,
            Latitude = (decimal)latitude,
            Longitude = (decimal)longitude,
            Sog = sog,
            NavStatus = navStatus
        };
    }

    private sealed class FakeAnalyticsRepository
        : IVesselAnalyticsRepository
    {
        private readonly IReadOnlyList<Position> _positions;

        public FakeAnalyticsRepository(
            IReadOnlyList<Position> positions)
        {
            _positions = positions;
        }

        public Task<IReadOnlyList<Position>> GetTrackAsync(
            long mmsi,
            DateTime from,
            DateTime to)
        {
            return Task.FromResult(_positions);
        }
    }
}