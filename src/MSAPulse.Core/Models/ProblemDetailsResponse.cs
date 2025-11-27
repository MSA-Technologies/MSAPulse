namespace MSAPulse.Core.Models
{
    /// <summary>
    /// Represents an error response formatted according to the RFC 7807 Problem Details specification.
    /// </summary>
    public class ProblemDetailsResponse
    {
        /// <summary>
        /// A URI reference that identifies the problem type (e.g., https://httpstatuses.com/400).
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// A short, human-readable summary of the problem type.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// The associated HTTP status code.
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// A human-readable explanation specific to this occurrence of the problem.
        /// </summary>
        public string? Detail { get; set; }

        /// <summary>
        /// A unique identifier used for tracing the error across logs.
        /// </summary>
        public string? TraceId { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the error occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Technical stack trace information (included only in development environments).
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// Validation errors or additional error details, if any.
        /// </summary>
        public IDictionary<string, string[]>? Errors { get; set; }
    }
}
