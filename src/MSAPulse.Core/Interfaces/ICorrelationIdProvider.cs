namespace MSAPulse.Core.Interfaces
{
    /// <summary>
    /// Provides access to the current Correlation ID used to trace a request across
    /// the application's execution pipeline.
    /// </summary>
    public interface ICorrelationIdProvider
    {
        /// <summary>
        /// Returns the current Correlation ID associated with the active execution context.
        /// </summary>
        string GetCorrelationId();

        /// <summary>
        /// Sets or overrides the Correlation ID for the current execution context.
        /// If no value is provided, a new ID should be generated.
        /// </summary>
        /// <param name="correlationId">Optional: an existing ID to reuse</param>
        void SetCorrelationId(string? correlationId = null);
    }
}
