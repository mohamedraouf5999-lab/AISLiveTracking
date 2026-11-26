using AISLiveTracking.API.Data.Interfaces;
using AISLiveTracking.API.Models;
using AISLiveTracking.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AISLiveTracking.API.Services;

public class VesselAnalyticsService : IVesselAnalyticsService
{
    private readonly IVesselAnalyticsRepository _analyticsRepository;
    private readonly AnalyticsOptions _options;

    public VesselAnalyticsService(
        IVesselAnalyticsRepository analyticsRepository,
        IOptions<AnalyticsOptions> options)
    {
        _analyticsRepository = analyticsRepository;
        _options = options.Value;
    }

    public async Task<VesselAnalyticsResult> GetAnalyticsAsync(
        long mmsi,
        DateTime from,
        DateTime to)
    {
        var positions = await _analyticsRepository.GetTrackAsync(
            mmsi,
            from,
            to);

        var result = new VesselAnalyticsResult
        {
            Mmsi = mmsi,
            Window = new AnalyticsWindow
            {
                From = from,
                To = to
            }
        };

        if (positions.Count == 0)
        {
            return result;
        }

        result.Reporting.Messages = positions.Count;
        result.Reporting.First = positions[0].MsgTimestampUtc;
        result.Reporting.Last = positions[^1].MsgTimestampUtc;

        var requestedWindowSeconds = (to - from).TotalSeconds;

        if (requestedWindowSeconds > 0)
        {
            var coveredSeconds = Math.Max(
                0,
                (positions[^1].MsgTimestampUtc -
                 positions[0].MsgTimestampUtc).TotalSeconds);

            result.Window.CoveragePercent = Math.Min(
                100,
                coveredSeconds / requestedWindowSeconds * 100);
        }

        var gapThreshold =
            TimeSpan.FromMinutes(_options.GapThresholdMinutes);

        var minimumIdleHours =
            _options.IdleMinimumMinutes / 60.0;

        var distanceKm = 0.0;
        var distanceExcludedKm = 0.0;
        var totalSogKnots = 0.0;
        var sogCount = 0;
        double? maxSogKnots = null;
        double? maxImpliedKnots = null;
        var stoppedRunHours = 0.0;

        var gapCount = 0;
        var totalDarkHours = 0.0;
        AnalyticsGap? longestGap = null;

        var intervals = new List<double>(
            Math.Max(positions.Count - 1, 0));

        void CompleteStoppedRun()
        {
            if (stoppedRunHours >= minimumIdleHours)
            {
                result.IdleHours += stoppedRunHours;
            }

            stoppedRunHours = 0;
        }

        for (var i = 0; i < positions.Count; i++)
        {
            var current = positions[i];

            if (current.Sog.HasValue)
            {
                var sogKnots = (double)current.Sog.Value;

                totalSogKnots += sogKnots;
                sogCount++;

                if (!maxSogKnots.HasValue ||
                    sogKnots > maxSogKnots.Value)
                {
                    maxSogKnots = sogKnots;
                }
            }

            if (i == 0)
            {
                continue;
            }

            var previous = positions[i - 1];
            var interval = current.MsgTimestampUtc -
                           previous.MsgTimestampUtc;

            if (interval <= TimeSpan.Zero)
            {
                continue;
            }

            var intervalSeconds = interval.TotalSeconds;
            var intervalHours = interval.TotalHours;

            intervals.Add(intervalSeconds);

            var segmentDistanceKm =
                GeoCalculator.HaversineDistanceKm(
                    (double)previous.Latitude,
                    (double)previous.Longitude,
                    (double)current.Latitude,
                    (double)current.Longitude);

            if (interval > gapThreshold)
            {
                distanceExcludedKm += segmentDistanceKm;
                gapCount++;
                totalDarkHours += intervalHours;

                if (longestGap == null ||
                    intervalHours > longestGap.Hours)
                {
                    longestGap = new AnalyticsGap
                    {
                        Hours = intervalHours,
                        From = previous.MsgTimestampUtc,
                        To = current.MsgTimestampUtc
                    };
                }

                CompleteStoppedRun();
                continue;
            }

            distanceKm += segmentDistanceKm;

            if (previous.Sog.HasValue)
            {
                if (previous.Sog.Value < _options.StopSpeedKnots)
                {
                    result.Time.StoppedHours += intervalHours;
                    stoppedRunHours += intervalHours;
                }
                else
                {
                    result.Time.UnderwayHours += intervalHours;
                    CompleteStoppedRun();
                }
            }
            else
            {
                CompleteStoppedRun();
            }

            if (previous.NavStatus.HasValue)
            {
                var navStatus = previous.NavStatus.Value;

                if (result.Time.ByNavStatus.TryGetValue(
                        navStatus,
                        out var existingHours))
                {
                    result.Time.ByNavStatus[navStatus] =
                        existingHours + intervalHours;
                }
                else
                {
                    result.Time.ByNavStatus[navStatus] =
                        intervalHours;
                }
            }

            var impliedKnots =
                segmentDistanceKm / intervalHours / 1.852;

            if (!maxImpliedKnots.HasValue ||
                impliedKnots > maxImpliedKnots.Value)
            {
                maxImpliedKnots = impliedKnots;
            }
        }

        CompleteStoppedRun();

        if (sogCount > 0)
        {
            result.Speed.AvgSogKnots =
                totalSogKnots / sogCount;

            result.Speed.MaxSogKnots = maxSogKnots;
        }

        result.Speed.MaxImpliedKnots = maxImpliedKnots;
        result.DistanceKm = distanceKm;
        result.DistanceExcludedKm = distanceExcludedKm;

        result.Gaps.Count = gapCount;
        result.Gaps.TotalDarkHours = totalDarkHours;
        result.Gaps.Longest = longestGap;

        if (intervals.Count > 0)
        {
            intervals.Sort();

            var middle = intervals.Count / 2;

            result.Reporting.MedianIntervalSeconds =
                intervals.Count % 2 == 0
                    ? (intervals[middle - 1] + intervals[middle]) / 2
                    : intervals[middle];
        }

        return result;
    }
}