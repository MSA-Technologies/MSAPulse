using System.Collections.Concurrent;
using MSAPulse.Core.Interfaces;
using MSAPulse.Core.Models;

namespace MSAPulse.Infrastructure.Services;

/// <summary>
/// Service that stores and manages performance metrics in memory.
/// Thread-safe implementation supporting concurrent access.
/// </summary>
public class PerformanceTracker : IPerformanceTracker
{
    private readonly ConcurrentBag<PerformanceMetric> _metrics = new();
    private readonly int _maxMetricsCount;

    /// <summary>
    /// PerformanceTracker constructor.
    /// </summary>
    /// <param name="maxMetricsCount">Maximum number of metrics to store in memory (default: 1000)</param>
    public PerformanceTracker(int maxMetricsCount = 1000)
    {
        _maxMetricsCount = maxMetricsCount;
    }

    /// <inheritdoc/>
    public void TrackMetric(PerformanceMetric metric)
    {
        if (metric == null)
        {
            throw new ArgumentNullException(nameof(metric));
        }

        if (_metrics.Count >= _maxMetricsCount)
        {
            ClearOldMetrics();
        }

        _metrics.Add(metric);
    }

    /// <inheritdoc/>
    public IEnumerable<PerformanceMetric> GetMetrics(string operationType)
    {
        if (string.IsNullOrWhiteSpace(operationType))
        {
            return _metrics.ToList();
        }

        return _metrics
            .Where(m => m.OperationType.Equals(operationType, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.Timestamp)
            .ToList();
    }

    /// <inheritdoc/>
    public void ClearMetrics()
    {
        _metrics.Clear();
    }

    /// <summary>
    /// Clears the oldest 30% of metrics.
    /// </summary>
    private void ClearOldMetrics()
    {
        var metricsToRemove = _metrics
            .OrderBy(m => m.Timestamp)
            .Take(_maxMetricsCount / 3)
            .ToList();

        var newBag = new ConcurrentBag<PerformanceMetric>(
            _metrics.Except(metricsToRemove)
        );

        _metrics.Clear();
        foreach (var metric in newBag)
        {
            _metrics.Add(metric);
        }
    }
}
