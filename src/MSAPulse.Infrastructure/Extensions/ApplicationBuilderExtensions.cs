using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MSAPulse.Infrastructure.Interceptors;
using MSAPulse.Infrastructure.Middleware;
using Serilog;

namespace MSAPulse.Infrastructure.Extensions
{
    /// <summary>
    /// Extension methods for configuring MSAPulse middleware and EF Core interceptors.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Registers all MSAPulse middleware components.
        /// IMPORTANT: This method must be invoked before other middleware in the pipeline.
        /// </summary>
        /// <param name="app">Application builder.</param>
        /// <returns>The application builder (fluent API).</returns>
        public static IApplicationBuilder UseMSAPulse(this IApplicationBuilder app)
        {
            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate =
                    "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";

                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                    diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
                    diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString());

                    if (httpContext.Response.Headers.ContainsKey("X-Correlation-ID"))
                    {
                        diagnosticContext.Set("CorrelationId",
                            httpContext.Response.Headers["X-Correlation-ID"].ToString());
                    }
                };

                options.GetLevel = (httpContext, elapsed, ex) =>
                {
                    if (ex != null)
                        return Serilog.Events.LogEventLevel.Error;

                    return httpContext.Response.StatusCode switch
                    {
                        >= 500 => Serilog.Events.LogEventLevel.Error,
                        >= 400 => Serilog.Events.LogEventLevel.Warning,
                        _ => Serilog.Events.LogEventLevel.Information
                    };
                };
            });

            app.UseMSAPulseExceptionHandling();

            app.UseMSAPulseCorrelationId();

            return app;
        }

        /// <summary>
        /// Registers the MSAPulse Performance Interceptor for EF Core command execution.
        /// </summary>
        public static DbContextOptionsBuilder AddMSAPulseInterceptor<TContext>(
            this DbContextOptionsBuilder optionsBuilder,
            IServiceProvider serviceProvider) where TContext : DbContext
        {
            var interceptor = serviceProvider.GetRequiredService<PerformanceDbCommandInterceptor>();
            optionsBuilder.AddInterceptors(interceptor);
            return optionsBuilder;
        }

        /// <summary>
        /// Enables MSAPulse's global exception handling only (minimal configuration).
        /// Correlation ID middleware is NOT included.
        /// </summary>
        public static IApplicationBuilder UseMSAPulseExceptionHandling(this IApplicationBuilder app)
        {
            app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
            return app;
        }

        /// <summary>
        /// Registers only the Correlation ID middleware.
        /// </summary>
        public static IApplicationBuilder UseMSAPulseCorrelationId(this IApplicationBuilder app)
        {
            app.UseMiddleware<MSAPulseContextMiddleware>();
            return app;
        }
    }
}
