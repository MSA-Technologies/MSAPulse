using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MSAPulse.Core.Interfaces;
using MSAPulse.Core.Models;
using MSAPulse.Infrastructure.Interceptors;
using MSAPulse.Infrastructure.Services;
using Serilog;
using Serilog.Events;

namespace MSAPulse.Infrastructure.Extensions;

/// <summary>
/// Extension methods for IServiceCollection.
/// This class is the main entry point for integrating MSAPulse into a project.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers and configures MSAPulse services.
    /// Supports hybrid configuration: both appsettings.json and runtime overrides (Action delegate).
    /// </summary>
    /// <param name="services">The .NET DI container</param>
    /// <param name="configuration">IConfiguration used to read appsettings.json</param>
    /// <param name="configureOptions">Optional override method for runtime settings (e.g. options.SlowQueryThresholdMs = 0)</param>
    /// <returns>IServiceCollection for fluent usage</returns>
    public static IServiceCollection AddMSAPulse(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<MSAPulseOptions>? configureOptions = null)
    {
        var options = new MSAPulseOptions();

        var section = configuration.GetSection("MSAPulse");
        if (section.Exists())
        {
            section.Bind(options);
        }

        configureOptions?.Invoke(options);

        services.AddSingleton(options);

        services.AddHttpContextAccessor();

        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();

        if (options.EnablePerformanceTracking)
        {
            services.AddSingleton<IPerformanceTracker, PerformanceTracker>();
        }

        services.AddScoped<PerformanceDbCommandInterceptor>();

        ConfigureSerilog(options);

        services.AddSerilog(dispose: true);

        return services;
    }

    /// <summary>
    /// Configures Serilog according to the provided options.
    /// </summary>
    private static void ConfigureSerilog(MSAPulseOptions options)
    {
        var logConfig = new LoggerConfiguration()
            .MinimumLevel.Is(ParseLogLevel(options.MinimumLogLevel))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)

            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()

            .WriteTo.Console(outputTemplate: "[MSAPulse] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        if (!string.IsNullOrWhiteSpace(options.LogFilePath))
        {
            logConfig.WriteTo.File(
                path: options.LogFilePath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }

        if (!string.IsNullOrWhiteSpace(options.SeqUrl))
        {
            logConfig.WriteTo.Seq(options.SeqUrl);
        }

        Log.Logger = logConfig.CreateLogger();
    }

    /// <summary>
    /// Converts log level string values to Serilog enum types.
    /// </summary>
    private static LogEventLevel ParseLogLevel(string level)
    {
        return level?.ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}