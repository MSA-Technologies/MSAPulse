using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSAPulse.Core.Interfaces;
using MSAPulse.Core.Models;

namespace MSAPulse.Infrastructure.Middleware;

/// <summary>
/// Global exception handler middleware - Catches all unhandled exceptions and returns a response in RFC 7807 format.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly MSAPulseOptions _options;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment,
        IOptions<MSAPulseOptions> options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdProvider correlationIdProvider)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, correlationIdProvider);
        }
    }

    /// <summary>
    /// Catches the exception, logs it, and returns a standardized problem details response.
    /// </summary>
    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception,
        ICorrelationIdProvider correlationIdProvider)
    {
        string correlationId;

        if (context.Response.Headers.TryGetValue(_options.CorrelationIdHeader, out var existingId))
        {
            correlationId = existingId.ToString();
        }
        else
        {
            correlationId = correlationIdProvider.GetCorrelationId();
        }

        _logger.LogError(exception,
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}",
            correlationId,
            context.Request.Path,
            context.Request.Method);

        var (statusCode, title) = DetermineStatusCode(exception);

        var problemDetails = new ProblemDetailsResponse
        {
            Type = "https://httpstatuses.com/" + (int)statusCode,
            Title = title,
            Status = (int)statusCode,
            TraceId = correlationId,
            Timestamp = DateTime.UtcNow
        };

        if (_environment.IsDevelopment() || _options.IncludeExceptionDetails)
        {
            problemDetails.Detail = exception.Message;
            problemDetails.StackTrace = exception.StackTrace;

            if (exception.InnerException != null)
            {
                problemDetails.Errors = new Dictionary<string, string[]>
                {
                    ["InnerException"] = new[] { exception.InnerException.Message }
                };
            }
        }
        else
        {
            problemDetails.Detail = "An error occurred while processing your request. Please contact support with the trace ID.";
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        if (!context.Response.Headers.ContainsKey(_options.CorrelationIdHeader))
        {
            context.Response.Headers.TryAdd(_options.CorrelationIdHeader, correlationId);
        }

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await context.Response.WriteAsync(json);
    }

    /// <summary>
    /// Determines HTTP status code and title based on the exception type.
    /// </summary>
    private static (HttpStatusCode statusCode, string title) DetermineStatusCode(Exception exception)
    {
        return exception switch
        {
            ArgumentException or ArgumentNullException =>
                (HttpStatusCode.BadRequest, "Invalid Request"),

            UnauthorizedAccessException =>
                (HttpStatusCode.Unauthorized, "Unauthorized"),

            KeyNotFoundException or FileNotFoundException =>
                (HttpStatusCode.NotFound, "Resource Not Found"),

            InvalidOperationException =>
                (HttpStatusCode.Conflict, "Operation Conflict"),

            TimeoutException =>
                (HttpStatusCode.RequestTimeout, "Request Timeout"),

            NotImplementedException =>
                (HttpStatusCode.NotImplemented, "Not Implemented"),

            _ => (HttpStatusCode.InternalServerError, "Internal Server Error")
        };
    }
}
