// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Tests.WebEngineState;

/// <summary>
/// <c>captureState</c> and <c>restoreState</c> of the web engine's session (<c>web/src/surface.ts</c>). These are .NET-only tests:
/// upstream reaches them only through its runner's setup sessions, which the port does not have.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class WebEngineStateTests
{
    // Shows the stored plan, and stores "pro" when the page is opened with ?set.
    private const string Page = """
        <!DOCTYPE html>
        <html><body>
        <button type="button" id="plan"></button>
        <script>
          if (location.search === "?set") localStorage.setItem("plan", "pro");
          document.getElementById("plan").textContent = "plan:" + (localStorage.getItem("plan") ?? "none");
        </script>
        </body></html>
        """;

    private static Task<IEngineSession> StartAsync(string url) =>
        new WebEngine(new WebEngineOptions()).StartAsync(new EngineStartOptions { BaseUrl = url, ActionTimeout = TimeSpan.FromSeconds(30) }, CancellationToken.None);

    private static async Task<string?> PlanAsync(IEngineSession session)
    {
        var observation = await session.ObserveAsync(CancellationToken.None);
        return Flatten(observation.Roots).FirstOrDefault(node => node.Role == "button")?.Name;
    }

    private static IEnumerable<SemanticNode> Flatten(IEnumerable<SemanticNode> nodes) =>
        nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));

    [Fact]
    public async Task Captures_a_session_s_storage_and_restores_it_into_another()
    {
        using var site = await TinySite.StartAsync(Page);
        EngineState state;
        await using (var saving = await StartAsync(site.Url))
        {
            await saving.OpenAsync(site.Url + "/?set", CancellationToken.None);
            Assert.Equal("plan:pro", await PlanAsync(saving));
            state = await saving.CaptureStateAsync(CancellationToken.None);
        }

        Assert.Equal("playwright-storage-state", state.Format);
        Assert.Equal(1, state.Version);

        await using var fresh = await StartAsync(site.Url);
        await fresh.OpenAsync(site.Url, CancellationToken.None);
        Assert.Equal("plan:none", await PlanAsync(fresh));

        await fresh.RestoreStateAsync(state, CancellationToken.None);
        await fresh.OpenAsync(site.Url, CancellationToken.None);
        Assert.Equal("plan:pro", await PlanAsync(fresh));
    }

    [Theory]
    [InlineData("other-format", 1, "unsupported state format other-format@1")]
    [InlineData("playwright-storage-state", 2, "unsupported state format playwright-storage-state@2")]
    public async Task Refuses_a_state_of_another_format_or_version(string format, int version, string message)
    {
        await using var session = await StartAsync("http://127.0.0.1:1/");

        var error = await Assert.ThrowsAsync<EngineException>(() => session.RestoreStateAsync(new EngineState(format, version, new JsonObject()), CancellationToken.None));
        Assert.Equal("INVALID_STATE", error.Code);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task Refuses_state_data_that_is_not_a_storage_state_object_so_a_string_is_never_read_as_a_path()
    {
        await using var session = await StartAsync("http://127.0.0.1:1/");

        foreach (var data in new JsonNode?[] { System.Text.Json.Nodes.JsonValue.Create("/etc/passwd"), new JsonObject { ["cookies"] = new JsonArray() }, null })
        {
            var error = await Assert.ThrowsAsync<EngineException>(() => session.RestoreStateAsync(new EngineState("playwright-storage-state", 1, data), CancellationToken.None));
            Assert.Equal("INVALID_STATE", error.Code);
            Assert.Equal("state data must be a storage-state object", error.Message);
        }
    }
}
