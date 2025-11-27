namespace MSAPulse.Core.Models
{
    /// <summary>
    /// Represents a performance metric collected for an application operation.
    /// </summary>
    public class PerformanceMetric
    {
        /// <summary>
        /// Unique identifier of the metric entry.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The category or type of the operation (e.g., "DatabaseQuery", "HttpRequest").
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Name or descriptive label of the operation.
        /// </summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>
        /// Duration of the operation in milliseconds.
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Timestamp indicating when the metric was recorded.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Correlation ID associated with the operation.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Indicates whether the operation completed successfully.
        /// </summary>
        public bool IsSuccessful { get; set; } = true;

        /// <summary>
        /// Error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Additional metadata for extended context.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
