using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSAPulse.Core.Interfaces;
using MSAPulse.Core.Models;
using Serilog.Context;

namespace MSAPulse.Infrastructure.Middleware;

/// <summary>
/// Middleware for managing Correlation ID and enriching the log context.
/// Provides a unique tracking ID for each HTTP request.
/// </summary>
public class MSAPulseContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly MSAPulseOptions _options;
    private readonly ILogger<MSAPulseContextMiddleware> _logger;

    public MSAPulseContextMiddleware(
        RequestDelegate next,
        IOptions<MSAPulseOptions> options,
        ILogger<MSAPulseContextMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdProvider correlationIdProvider)
    {
        try
        {
            var correlationId = ExtractOrGenerateCorrelationId(context);

            correlationIdProvider.SetCorrelationId(correlationId);

            if (!context.Response.Headers.ContainsKey(_options.CorrelationIdHeader))
            {
                context.Response.Headers.Append(_options.CorrelationIdHeader, correlationId);
            }

            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("RequestPath", context.Request.Path))
            using (LogContext.PushProperty("RequestMethod", context.Request.Method))
            using (LogContext.PushProperty("UserAgent", context.Request.Headers["User-Agent"].ToString()))
            using (LogContext.PushProperty("RemoteIpAddress", context.Connection.RemoteIpAddress?.ToString()))
            {
                await _next(context);
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Extracts the Correlation ID from HTTP headers or generates a new one.
    /// </summary>
    private string ExtractOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(_options.CorrelationIdHeader, out var correlationId)
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }

        if (context.Request.Headers.TryGetValue("traceparent", out var traceParent)
            && !string.IsNullOrWhiteSpace(traceParent))
        {
            return ExtractTraceId(traceParent.ToString());
        }

        if (context.Request.Headers.TryGetValue("Request-Id", out var requestId)
            && !string.IsNullOrWhiteSpace(requestId))
        {
            return requestId.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Extracts trace ID from W3C Trace Context format.
    /// Format: 00-{trace-id}-{parent-id}-{trace-flags}
    /// </summary>
    private string ExtractTraceId(string traceParent)
    {
        try
        {
            var parts = traceParent.Split('-');
            if (parts.Length >= 2)
            {
                return parts[1]; 
            }
        }
        catch
        {
        }

        return Guid.NewGuid().ToString("N");
    }
}
