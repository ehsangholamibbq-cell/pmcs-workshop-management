using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> VerifyV11LoadSoakAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var timings = new ConcurrentBag<double>();
        var failures = new ConcurrentBag<string>();
        const int workerCount = 2;
        var clock = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, workerCount).Select(async worker =>
        {
            var iteration = 0;
            while (clock.Elapsed < TimeSpan.FromSeconds(30))
            {
                var operation = (worker + iteration) % 3;
                var path = operation switch
                {
                    0 => "/api/qa/v1/diagnostics",
                    1 => Preview("observer", "project-state.read"),
                    _ => Preview("technical-office", "finance.records.review")
                };
                var requestClock = Stopwatch.StartNew();
                try
                {
                    var result = await SendAsync(client, key, PmcsTestDataSet.QaSuperAdministrator,
                        HttpMethod.Get, path);
                    if (result.StatusCode != HttpStatusCode.OK || !IsExpected(operation, result.Payload))
                        failures.Add($"response:{operation}:{(int)result.StatusCode}");
                }
                catch (Exception exception) when (exception is HttpRequestException or
                    TaskCanceledException or JsonException)
                {
                    failures.Add($"transport:{operation}:{exception.GetType().Name}");
                }
                timings.Add(requestClock.Elapsed.TotalMilliseconds);
                iteration++;
                await Task.Delay(550);
            }
        });
        await Task.WhenAll(tasks);
        clock.Stop();

        var ordered = timings.Order().ToArray();
        var p95 = ordered.Length == 0 ? double.PositiveInfinity :
            ordered[(int)Math.Ceiling(ordered.Length * 0.95) - 1];
        var passed = clock.Elapsed >= TimeSpan.FromSeconds(30) && ordered.Length >= 80 &&
            failures.IsEmpty && p95 < 1500;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "pmcs-v1.1-permission-diagnostics-load-soak",
            status = passed ? "passed" : "failed",
            durationSeconds = Math.Round(clock.Elapsed.TotalSeconds, 3),
            workers = workerCount,
            requests = ordered.Length,
            failedRequests = failures.Count,
            p95Milliseconds = Math.Round(p95, 3),
            p95BudgetMilliseconds = 1500,
            requestBudgetMinimum = 80
        }));
        return passed ? 0 : 1;
    }

    private static string Preview(string actor, string permission) =>
        $"/api/qa/v1/permissions/preview?projectId={PmcsTestDataSet.ProjectId}" +
        $"&userId={Actor(actor).UserId}&operation={Uri.EscapeDataString(permission)}";

    private static bool IsExpected(int operation, JsonElement payload) => operation switch
    {
        0 => TryReadString(payload, "overallHealth", out var health) && health == "Healthy",
        1 => TryReadDecision(payload, "project-state.read", out var allowedRead) && allowedRead,
        _ => TryReadDecision(payload, "finance.records.review", out var allowedDenied) && !allowedDenied
    };
}
