namespace MSAPulse.Core.Models
{
    /// <summary>
    /// Configuration options for the MSAPulse library.
    /// </summary>
    public class MSAPulseOptions
    {
        /// <summary>
        /// The HTTP header used to read or propagate the Correlation ID.
        /// Default: "X-Correlation-ID"
        /// </summary>
        public string CorrelationIdHeader { get; set; } = "X-Correlation-ID";

        /// <summary>
        /// Threshold (in milliseconds) for marking a database query as slow.
        /// Queries exceeding this value will be logged at WARNING level.
        /// Default: 500ms
        /// </summary>
        public int SlowQueryThresholdMs { get; set; } = 500;

        /// <summary>
        /// Indicates whether detailed exception messages should be returned.
        /// Should be disabled in production environments.
        /// Default: false
        /// </summary>
        public bool IncludeExceptionDetails { get; set; } = false;

        /// <summary>
        /// Enables or disables in-memory performance metric tracking.
        /// Default: true
        /// </summary>
        public bool EnablePerformanceTracking { get; set; } = true;

        /// <summary>
        /// Enables logging of HTTP request and response bodies.
        /// Use cautiously when dealing with sensitive data.
        /// Default: false
        /// </summary>
        public bool LogRequestResponseBodies { get; set; } = false;

        /// <summary>
        /// Minimum log level to be used by Serilog.
        /// Default: "Information"
        /// </summary>
        public string MinimumLogLevel { get; set; } = "Information";

        /// <summary>
        /// Directory path where log files will be written.
        /// If null, logs are written only to the console.
        /// </summary>
        public string? LogFilePath { get; set; }

        /// <summary>
        /// Seq server URL for structured log ingestion.
        /// </summary>
        public string? SeqUrl { get; set; }

        /// <summary>
        /// Validates the configuration and applies defaults where necessary.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(CorrelationIdHeader))
            {
                throw new ArgumentException("CorrelationIdHeader cannot be null or empty.", nameof(CorrelationIdHeader));
            }

            if (SlowQueryThresholdMs < 0)
            {
                throw new ArgumentException("SlowQueryThresholdMs cannot be negative.", nameof(SlowQueryThresholdMs));
            }

            if (string.IsNullOrWhiteSpace(MinimumLogLevel))
            {
                MinimumLogLevel = "Information";
            }
        }
    }
}
