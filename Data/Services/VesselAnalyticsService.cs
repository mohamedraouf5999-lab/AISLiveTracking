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
            var coveredSeconds =
                Math.Max(
                    0,
                    (positions[^1].MsgTimestampUtc -
                     positions[0].MsgTimestampUtc).TotalSeconds);

            result.Window.CoveragePercent =
                Math.Min(
                    100,
                    coveredSeconds / requestedWindowSeconds * 100);
        }

        var sogValues = positions
            .Where(p => p.Sog.HasValue)
            .Select(p => (double)p.Sog!.Value)
            .ToList();

        if (sogValues.Count > 0)
        {
            result.Speed.AvgSogKnots = sogValues.Average();
            result.Speed.MaxSogKnots = sogValues.Max();
        }

        var gapThreshold = TimeSpan.FromMinutes(
            _options.GapThresholdMinutes);

        double distanceKm = 0;
        double distanceExcludedKm = 0;
        double? maxImpliedKnots = null;

        AnalyticsGap? longestGap = null;
        var gapCount = 0;
        var totalDarkHours = 0.0;

        double stoppedRunHours = 0;


        for (var i = 1; i < positions.Count; i++)
        {
            var previous = positions[i - 1];
            var current = positions[i];

            var interval = current.MsgTimestampUtc -
                           previous.MsgTimestampUtc;

            if (interval <= TimeSpan.Zero)
            {
                continue;
            }

            var segmentDistanceKm = GeoCalculator.HaversineDistanceKm(
                (double)previous.Latitude,
                (double)previous.Longitude,
                (double)current.Latitude,
                (double)current.Longitude);

            if (interval > gapThreshold)
            {
                distanceExcludedKm += segmentDistanceKm;

                gapCount++;

                var darkHours = interval.TotalHours;
                totalDarkHours += darkHours;

                if (longestGap == null ||
                    darkHours > longestGap.Hours)
                {
                    longestGap = new AnalyticsGap
                    {
                        Hours = darkHours,
                        From = previous.MsgTimestampUtc,
                        To = current.MsgTimestampUtc
                    };
                }

                if (stoppedRunHours >=
                    _options.IdleMinimumMinutes / 60.0)
                {
                    result.IdleHours += stoppedRunHours;
                }

                stoppedRunHours = 0;

                continue;
            }

            distanceKm += segmentDistanceKm;

            var segmentHours = interval.TotalHours;

            if (previous.Sog.HasValue)
            {
                if (previous.Sog.Value < _options.StopSpeedKnots)
                {
                    result.Time.StoppedHours += segmentHours;
                    stoppedRunHours += segmentHours;
                }
                else
                {
                    result.Time.UnderwayHours += segmentHours;

                    if (stoppedRunHours >=
                        _options.IdleMinimumMinutes / 60.0)
                    {
                        result.IdleHours += stoppedRunHours;
                    }

                    stoppedRunHours = 0;
                }
            }
            else
            {
                if (stoppedRunHours >=
                    _options.IdleMinimumMinutes / 60.0)
                {
                    result.IdleHours += stoppedRunHours;
                }

                stoppedRunHours = 0;
            }

            if (previous.NavStatus.HasValue)
            {
                var navStatus = previous.NavStatus.Value;

                if (result.Time.ByNavStatus.ContainsKey(navStatus))
                {
                    result.Time.ByNavStatus[navStatus] += segmentHours;
                }
                else
                {
                    result.Time.ByNavStatus[navStatus] = segmentHours;
                }
            }

            var impliedKnots =
                segmentDistanceKm /
                interval.TotalHours /
                1.852;

            if (!maxImpliedKnots.HasValue ||
                impliedKnots > maxImpliedKnots.Value)
            {
                maxImpliedKnots = impliedKnots;
            }
        }

        // Finalize a stopped run that continues until the end of the window.
        if (stoppedRunHours >= _options.IdleMinimumMinutes / 60.0)
        {
            result.IdleHours += stoppedRunHours;
        }

        result.DistanceKm = distanceKm;
        result.DistanceExcludedKm = distanceExcludedKm;

        result.Speed.MaxImpliedKnots = maxImpliedKnots;

        result.Gaps.Count = gapCount;
        result.Gaps.TotalDarkHours = totalDarkHours;
        result.Gaps.Longest = longestGap;

        var intervals = positions
            .Zip(
                positions.Skip(1),
                (previous, current) =>
                    (current.MsgTimestampUtc -
                     previous.MsgTimestampUtc).TotalSeconds)
            .Where(seconds => seconds > 0)
            .OrderBy(seconds => seconds)
            .ToList();

        if (intervals.Count > 0)
        {
            var middle = intervals.Count / 2;

            result.Reporting.MedianIntervalSeconds =
                intervals.Count % 2 == 0
                    ? (intervals[middle - 1] + intervals[middle]) / 2
                    : intervals[middle];
        }

        return result;
    }
}