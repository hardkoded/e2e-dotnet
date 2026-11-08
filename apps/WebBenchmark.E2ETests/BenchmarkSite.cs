// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using WebBenchmark;

namespace WebBenchmark.E2ETests;

/// <summary>
/// Starts <see cref="BenchmarkApp"/> once, on a free loopback port, before the first test of the run and stops it after the last.
/// Upstream's <c>e2e.config.ts</c> starts <c>pnpm run start</c> on port 4280 (<c>app.command</c>); the NUnit adapter does not
/// start <c>app.command</c>, so the port hosts the app in process (.NET-only).
/// </summary>
[SetUpFixture]
public sealed class BenchmarkSite
{
    private static WebApplication? _app;
    private static string? _url;

    /// <summary>The address the app answers at, for the tests of the run.</summary>
    public static string Url => _url ?? throw new InvalidOperationException("the benchmark app has not been started");

    [OneTimeSetUp]
    public async Task StartAppAsync()
    {
        var app = BenchmarkApp.Create(["--urls", "http://127.0.0.1:0"]);
        try
        {
            await app.StartAsync();
            _url = app.Urls.First();
            _app = app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    [OneTimeTearDown]
    public async Task StopAppAsync()
    {
        var app = _app;
        _app = null;
        _url = null;
        if (app is null)
        {
            return;
        }

        try
        {
            await app.StopAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await app.DisposeAsync();
        }
    }
}
