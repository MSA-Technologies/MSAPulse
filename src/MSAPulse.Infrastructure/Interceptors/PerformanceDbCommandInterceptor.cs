using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSAPulse.Core.Interfaces;
using MSAPulse.Core.Models;
using System.Data.Common;
using System.Diagnostics;

namespace MSAPulse.Infrastructure.Interceptors;

/// <summary>
/// Interceptor that monitors EF Core database commands and collects performance metrics.
/// Automatically logs slow queries and errors.
/// KILLER FEATURE - Database performance monitoring
/// </summary>
public class PerformanceDbCommandInterceptor : DbCommandInterceptor
{
    private readonly ILogger<PerformanceDbCommandInterceptor> _logger;
    private readonly MSAPulseOptions _options;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly IPerformanceTracker _performanceTracker;

    public PerformanceDbCommandInterceptor(
        ILogger<PerformanceDbCommandInterceptor> logger,
        MSAPulseOptions options,
        ICorrelationIdProvider correlationIdProvider,
        IPerformanceTracker performanceTracker)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _correlationIdProvider = correlationIdProvider ?? throw new ArgumentNullException(nameof(correlationIdProvider));
        _performanceTracker = performanceTracker ?? throw new ArgumentNullException(nameof(performanceTracker));
    }

    #region Async Methods (Primary)

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        await LogCommandExecutionAsync(command, eventData, null);
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        await LogCommandExecutionAsync(command, eventData, null);
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await LogCommandExecutionAsync(command, eventData, null);
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    #endregion

    #region Error Handling

    public override async Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await LogCommandExecutionAsync(command, eventData, eventData.Exception);
        await base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        LogCommandExecutionAsync(command, eventData, eventData.Exception).GetAwaiter().GetResult();
        base.CommandFailed(command, eventData);
    }

    #endregion

    #region Sync Methods (Fallback)

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        LogCommandExecutionAsync(command, eventData, null).GetAwaiter().GetResult();
        return base.ReaderExecuted(command, eventData, result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        LogCommandExecutionAsync(command, eventData, null).GetAwaiter().GetResult();
        return base.ScalarExecuted(command, eventData, result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        LogCommandExecutionAsync(command, eventData, null).GetAwaiter().GetResult();
        return base.NonQueryExecuted(command, eventData, result);
    }

    #endregion

    /// <summary>
    /// Measures and logs the execution time of the database command.
    /// </summary>
    private async Task LogCommandExecutionAsync(
        DbCommand command,
        CommandEventData eventData,
        Exception? exception)
    {
        double duration = 0;

        if (eventData is CommandExecutedEventData executedData)
        {
            duration = executedData.Duration.TotalMilliseconds;
        }
        else if (eventData is CommandErrorEventData errorData)
        {
            duration = errorData.Duration.TotalMilliseconds;
        }

        var correlationId = _correlationIdProvider.GetCorrelationId();
        var isSlowQuery = duration > _options.SlowQueryThresholdMs;
        var hasError = exception != null;

        if (_options.EnablePerformanceTracking)
        {
            var metric = new PerformanceMetric
            {
                OperationType = "DatabaseQuery",
                OperationName = ExtractQueryType(command.CommandText),
                DurationMs = (long)duration,
                CorrelationId = correlationId,
                IsSuccessful = !hasError,
                ErrorMessage = exception?.Message,
                Metadata = new Dictionary<string, object>
                {
                    ["CommandType"] = command.CommandType.ToString(),
                    ["Database"] = eventData.Context?.Database.GetDbConnection().Database ?? "Unknown"
                }
            };

            _performanceTracker.TrackMetric(metric);
        }

        if (hasError)
        {
            _logger.LogError(exception,
                "Database query FAILED | Duration: {Duration}ms | CorrelationId: {CorrelationId}\n" +
                "Query: {Query}\n" +
                "Parameters: {Parameters}",
                duration,
                correlationId,
                SanitizeQuery(command.CommandText),
                GetParametersString(command));
        }
        else if (isSlowQuery)
        {
            _logger.LogWarning(
                "SLOW QUERY DETECTED | Duration: {Duration}ms (Threshold: {Threshold}ms) | CorrelationId: {CorrelationId}\n" +
                "Query: {Query}\n" +
                "Parameters: {Parameters}\n" +
                "Suggestion: Consider adding indexes or optimizing this query",
                duration,
                _options.SlowQueryThresholdMs,
                correlationId,
                SanitizeQuery(command.CommandText),
                GetParametersString(command));
        }
        else
        {
            _logger.LogDebug(
                "Database query executed | Duration: {Duration}ms | Type: {Type} | CorrelationId: {CorrelationId}",
                duration,
                ExtractQueryType(command.CommandText),
                correlationId);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Extracts the query type from the SQL command (SELECT, INSERT, UPDATE, DELETE).
    /// </summary>
    private static string ExtractQueryType(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            return "Unknown";

        var normalized = commandText.TrimStart().ToUpperInvariant();

        if (normalized.StartsWith("SELECT")) return "SELECT";
        if (normalized.StartsWith("INSERT")) return "INSERT";
        if (normalized.StartsWith("UPDATE")) return "UPDATE";
        if (normalized.StartsWith("DELETE")) return "DELETE";
        if (normalized.StartsWith("EXEC") || normalized.StartsWith("EXECUTE")) return "PROCEDURE";

        return "Other";
    }

    /// <summary>
    /// Sanitizes the SQL query (truncates very long queries).
    /// </summary>
    private static string SanitizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        const int maxLength = 1000;
        if (query.Length > maxLength)
        {
            return query.Substring(0, maxLength) + "... (truncated)";
        }

        return query;
    }

    /// <summary>
    /// Returns SQL parameters in string format.
    /// </summary>
    private static string GetParametersString(DbCommand command)
    {
        if (command.Parameters.Count == 0)
            return "No parameters";

        var parameters = new List<string>();
        foreach (DbParameter parameter in command.Parameters)
        {
            var value = parameter.Value switch
            {
                null => "NULL",
                DBNull => "NULL",
                string s => $"'{s}'",
                _ => parameter.Value.ToString()
            };

            parameters.Add($"{parameter.ParameterName}={value}");
        }

        return string.Join(", ", parameters);
    }
}