// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit;
using Microsoft.AspNetCore.Builder;
using WebBenchmark;

namespace WebBenchmark.E2ETests.Fixtures;

/// <summary>
/// The suite's base class: <see cref="E2ETest"/> against the benchmark app, which the suite serves itself. Upstream's
/// <c>e2e.config.ts</c> starts <c>pnpm run start</c> on port 4280 (<c>app.command</c>); the port hosts the app in
/// process on a free loopback port for the whole run and points <see cref="BaseUrl"/> at it (.NET-only: the NUnit
/// adapter does not start <c>app.command</c>).
/// </summary>
public abstract class BenchmarkTest : E2ETest
{
    protected override string? BaseUrl => BenchmarkSite.Url.Value;
}

/// <summary>Starts <see cref="BenchmarkApp"/> once, on a free loopback port, and stops it when the test run ends.</summary>
internal static class BenchmarkSite
{
    public static readonly Lazy<string> Url = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    private static string Start()
    {
        var app = BenchmarkApp.Create(["--urls", "http://127.0.0.1:0"]);
        try
        {
            app.StartAsync().GetAwaiter().GetResult();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop(app);
            return app.Urls.First();
        }
        catch
        {
            Stop(app);
            throw;
        }
    }

    private static void Stop(WebApplication app)
    {
        try
        {
            app.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        finally
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
