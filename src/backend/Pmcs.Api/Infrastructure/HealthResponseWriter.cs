using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Pmcs.Api.Infrastructure;

internal static class HealthResponseWriter
{
    private static readonly HashSet<string> PublicNumericDataKeys = new(StringComparer.Ordinal)
    {
        "activeRuns",
        "heartbeatAgeSeconds",
        "oldestQueueAgeSeconds",
        "queuedRuns"
    };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            durationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    data = SelectPublicData(entry.Key, entry.Value.Data)
                })
        }));
    }

    internal static IReadOnlyDictionary<string, object> SelectPublicData(
        string checkName,
        IReadOnlyDictionary<string, object> data)
    {
        var selected = new SortedDictionary<string, object>(StringComparer.Ordinal);
        if (!string.Equals(checkName, "reporting-worker", StringComparison.Ordinal))
        {
            return selected;
        }

        foreach (var item in data)
        {
            if (PublicNumericDataKeys.Contains(item.Key) && IsNumeric(item.Value))
            {
                selected[item.Key] = item.Value;
            }
        }
        return selected;
    }

    private static bool IsNumeric(object value) => value is
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
