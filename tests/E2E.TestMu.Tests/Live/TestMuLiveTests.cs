// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using E2E.Engine;

namespace E2E.TestMu.Tests.Live;

/// <summary>Runs only when LT_USERNAME and LT_ACCESS_KEY are set: a skipped test, not a failure, without them.</summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LT_USERNAME")) || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LT_ACCESS_KEY")))
        {
            Skip = "LT_USERNAME and LT_ACCESS_KEY are not set.";
        }
    }
}

/// <summary>
/// Against the real TestMu grid, through the engine. This is the check of the two things the unit tests cannot: that the
/// grid takes the Playwright protocol the lease names (a CDP attach on its URL fails), and that it ends the session once the
/// engine closes the connection, which release relies on. The grid does not say the session id at connect, so the session
/// is found in the Automation API by its build and name. .NET-only.
/// </summary>
[Trait("TestCategory", "TestMu")]
public sealed class TestMuLiveTests
{
    private const string SessionsUrl = "https://api.lambdatest.com/automation/api/v1/sessions";

    [LiveFact]
    public async Task Drives_a_page_through_the_engine_and_the_grid_ends_the_session_when_the_connection_closes()
    {
        var user = Environment.GetEnvironmentVariable("LT_USERNAME")!;
        var accessKey = Environment.GetEnvironmentVariable("LT_ACCESS_KEY")!;
        var build = "e2e-live-" + Guid.NewGuid().ToString("n")[..12];
        var engine = new WebEngine(new WebEngineOptions { Browser = new TestMuBrowsers(new TestMuBrowsersOptions { Build = build }) });
        var env = new Dictionary<string, string?> { ["LT_USERNAME"] = user, ["LT_ACCESS_KEY"] = accessKey };

        await using (var session = await engine.StartAsync(new EngineStartOptions { BaseUrl = "https://example.com/", ActionTimeout = TimeSpan.FromSeconds(60), Env = env }, CancellationToken.None))
        {
            await session.OpenAsync("https://example.com/", CancellationToken.None);
            var observation = await session.ObserveAsync(CancellationToken.None);
            Assert.Equal("/", observation.Route);
            Assert.NotEmpty(observation.Roots);
        }

        // The session is the one of this build, named after the target and slot the engine leased.
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + accessKey)));
        var ended = await WaitForEndedSessionAsync(http, build, "web slot 1 of 1", TimeSpan.FromSeconds(90));
        Assert.True(ended, "the grid did not end the session after the connection closed");
    }

    /// <summary>Polls the sessions list, which is eventually consistent, until the build's session shows an end time.</summary>
    private static async Task<bool> WaitForEndedSessionAsync(HttpClient http, string build, string name, TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < deadline)
        {
            using var document = JsonDocument.Parse(await http.GetStringAsync(new Uri(SessionsUrl + "?limit=50")));
            if (document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in data.EnumerateArray())
                {
                    if (Text(entry, "build_name") == build && Text(entry, "name") == name && !string.IsNullOrEmpty(Text(entry, "end_timestamp")))
                    {
                        return true;
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        return false;
    }

    private static string? Text(JsonElement entry, string property) =>
        entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
