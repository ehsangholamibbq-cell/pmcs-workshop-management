using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Routing;

namespace Pmcs.Api.Infrastructure;

internal sealed partial class RequestTelemetryMiddleware(
    RequestDelegate next,
    ILogger<RequestTelemetryMiddleware> logger)
{
    internal const string MeterName = "Pmcs.Api";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RequestCounter =
        Meter.CreateCounter<long>("pmcs.api.requests", unit: "{request}");
    private static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("pmcs.api.request.duration", unit: "ms");

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var method = context.Request.Method;
            var statusCode = context.Response.StatusCode;

            RequestCounter.Add(
                1,
                new KeyValuePair<string, object?>("http.request.method", method),
                new KeyValuePair<string, object?>("http.route", route),
                new KeyValuePair<string, object?>("http.response.status_code", statusCode));
            RequestDuration.Record(
                elapsedMs,
                new KeyValuePair<string, object?>("http.request.method", method),
                new KeyValuePair<string, object?>("http.route", route),
                new KeyValuePair<string, object?>("http.response.status_code", statusCode));

            Activity.Current?.SetTag("pmcs.correlation_id", context.TraceIdentifier);
            LogRequestCompleted(
                logger,
                method,
                route,
                statusCode,
                elapsedMs,
                context.TraceIdentifier);
        }
    }

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Information,
        Message = "HTTP {Method} {Route} returned {StatusCode} in {ElapsedMs:F1} ms; correlation id {CorrelationId}.")]
    private static partial void LogRequestCompleted(
        ILogger logger,
        string method,
        string route,
        int statusCode,
        double elapsedMs,
        string correlationId);
}
