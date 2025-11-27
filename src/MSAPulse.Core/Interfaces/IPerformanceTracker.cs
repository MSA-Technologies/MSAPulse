using MSAPulse.Core.Models;

namespace MSAPulse.Core.Interfaces
{
    /// <summary>
    /// Provides a mechanism for tracking and aggregating performance metrics
    /// for database operations and other system activities.
    /// </summary>
    public interface IPerformanceTracker
    {
        /// <summary>
        /// Records a performance metric instance.
        /// </summary>
        /// <param name="metric">The metric to be recorded.</param>
        void TrackMetric(PerformanceMetric metric);

        /// <summary>
        /// Retrieves all collected metrics associated with the specified operation type.
        /// </summary>
        /// <param name="operationType">The category of the operation (e.g. "DatabaseQuery").</param>
        /// <returns>A collection of recorded metrics.</returns>
        IEnumerable<PerformanceMetric> GetMetrics(string operationType);

        /// <summary>
        /// Clears all tracked metrics from the internal store.
        /// </summary>
        void ClearMetrics();
    }
}
