using MSAPulse.Core.Interfaces;

namespace MSAPulse.Infrastructure.Services;

/// <summary>
/// Implementation for managing Correlation IDs.
/// Stores the ID in a thread-safe manner using AsyncLocal.
/// </summary>
public class CorrelationIdProvider : ICorrelationIdProvider
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <inheritdoc/>
    public string GetCorrelationId()
    {
        return _correlationId.Value ?? GenerateNewCorrelationId();
    }

    /// <inheritdoc/>
    public void SetCorrelationId(string? correlationId = null)
    {
        _correlationId.Value = string.IsNullOrWhiteSpace(correlationId)
            ? GenerateNewCorrelationId()
            : correlationId;
    }

    /// <summary>
    /// Generates a new unique Correlation ID.
    /// </summary>
    private static string GenerateNewCorrelationId()
    {
        return Guid.NewGuid().ToString("N");
    }
}
