// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.TestMu.Tests.Browsers;

/// <summary>
/// Ports the <c>it</c>s of the <c>describe('testmuBrowsers()')</c> in upstream's <c>browsers.test.ts</c> that cover the Edge
/// flag and the capability validation. The class is not named <c>TestmuBrowsersTests</c>, because that differs from
/// <see cref="TestMuBrowsersTests"/> only in case.
/// </summary>
public sealed class TestMuBrowsersFunctionTests
{
    private static async Task<JsonNode> LtOptions(TestMuBrowsersOptions options) =>
        TestMuBrowsersTests.Capabilities(await new TestMuBrowsers(options).AcquireAsync(TestMuBrowsersTests.Request(), CancellationToken.None))["LT:Options"]!;

    /// <summary>
    /// Partial: upstream sends <c>browserVersion</c> in <c>capabilities</c> and expects the <c>e2e-run-1-chromium-1</c>
    /// session name and the build <c>run-1</c>. The port has a <c>BrowserVersion</c> option and defaults the build to
    /// <c>e2e-&lt;run id&gt;</c>.
    /// </summary>
    [Fact]
    public async Task Partial_Sends_Edge_browserVersion_next_to_browserName_and_the_rest_as_LT_Options_which_override_the_build_and_project_defaults()
    {
        var provider = new TestMuBrowsers(new TestMuBrowsersOptions
        {
            BrowserName = "MicrosoftEdge",
            BrowserVersion = "140",
            Platform = "macOS Sequoia",
            Capabilities = new Dictionary<string, JsonNode?> { ["build"] = "nightly", ["project"] = "checkout", ["video"] = true, ["idleTimeout"] = 300 },
        });

        var capabilities = TestMuBrowsersTests.Capabilities(await provider.AcquireAsync(TestMuBrowsersTests.Request(), CancellationToken.None));

        Assert.Equal("MicrosoftEdge", (string?)capabilities["browserName"]);
        Assert.Equal("140", (string?)capabilities["browserVersion"]);
        Assert.Equal(
            """{"platform":"macOS Sequoia","project":"checkout","build":"nightly","idleTimeout":300,"name":"e2e-run-a-web-1","user":"ada","accessKey":"secret key","video":true,"browserOptions":["--user-data-dir=remote-debug-profile-edge"]}""",
            capabilities["LT:Options"]!.ToJsonString());
    }

    [Fact]
    public async Task Names_every_session_e2e_run_id_target_slot_as_testmu_does()
    {
        static async Task<string?> NameOf(string runId, string targetName, int slot, int slots)
        {
            var request = TestMuBrowsersTests.Request(slot: slot, slots: slots) with { RunId = runId, TargetName = targetName };
            var lease = await new TestMuBrowsers().AcquireAsync(request, CancellationToken.None);
            return (string?)TestMuBrowsersTests.Capabilities(lease)["LT:Options"]!["name"];
        }

        Assert.Equal("e2e-01a0fcab-e8da-checkout-web-1", await NameOf("01a0fcab-e8da", "checkout-web", 0, 1));
        Assert.Equal("e2e-01a0fcab-e8da-checkout-web-3", await NameOf("01a0fcab-e8da", "checkout-web", 2, 3));
    }

    [Fact]
    public async Task Fails_the_lease_when_a_credential_is_missing_or_blank()
    {
        static Task<InvalidOperationException> Acquire(Dictionary<string, string?> env) =>
            Assert.ThrowsAsync<InvalidOperationException>(() => new TestMuBrowsers().AcquireAsync(TestMuBrowsersTests.Request(env), CancellationToken.None));

        Assert.Contains("LT_ACCESS_KEY is not set", (await Acquire(new() { ["LT_USERNAME"] = "alice" })).Message, StringComparison.Ordinal);
        Assert.Contains("LT_USERNAME is not set", (await Acquire(new() { ["LT_USERNAME"] = " ", ["LT_ACCESS_KEY"] = "key" })).Message, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", (await Acquire(new() { ["LT_ACCESS_KEY"] = "key-secret" })).Message, StringComparison.Ordinal);
        Assert.DoesNotContain("key-secret", (await Acquire(new() { ["LT_ACCESS_KEY"] = "key-secret" })).Message, StringComparison.Ordinal);
        Assert.Contains("LT_USERNAME and LT_ACCESS_KEY are not set", (await Acquire([])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Adds_user_data_dir_remote_debug_profile_edge_to_an_Edge_sessions_browserOptions()
    {
        var lt = await LtOptions(new TestMuBrowsersOptions { BrowserName = "MicrosoftEdge" });

        Assert.Equal("""["--user-data-dir=remote-debug-profile-edge"]""", lt["browserOptions"]!.ToJsonString());
    }

    [Fact]
    public async Task Appends_the_Edge_user_data_dir_flag_to_the_browserOptions_capabilities_give()
    {
        var given = new JsonArray("--no-sandbox");

        var lt = await LtOptions(new TestMuBrowsersOptions { BrowserName = "MicrosoftEdge", Capabilities = new Dictionary<string, JsonNode?> { ["browserOptions"] = given } });

        Assert.Equal("""["--no-sandbox","--user-data-dir=remote-debug-profile-edge"]""", lt["browserOptions"]!.ToJsonString());
        Assert.Equal("""["--no-sandbox"]""", given.ToJsonString());
    }

    [Fact]
    public async Task Keeps_a_user_data_dir_that_capabilities_set_for_Edge()
    {
        var lt = await LtOptions(new TestMuBrowsersOptions { BrowserName = "MicrosoftEdge", Capabilities = new Dictionary<string, JsonNode?> { ["browserOptions"] = new JsonArray("--user-data-dir=mine") } });

        Assert.Equal("""["--user-data-dir=mine"]""", lt["browserOptions"]!.ToJsonString());
    }

    [Fact]
    public async Task Passes_Chrome_browserOptions_through_and_adds_none()
    {
        var plain = (await LtOptions(new TestMuBrowsersOptions())).AsObject();
        var given = await LtOptions(new TestMuBrowsersOptions { Capabilities = new Dictionary<string, JsonNode?> { ["browserOptions"] = new JsonArray("--no-sandbox") } });

        Assert.False(plain.ContainsKey("browserOptions"));
        Assert.Equal("""["--no-sandbox"]""", given["browserOptions"]!.ToJsonString());
    }

    private sealed record Wrapped(double Value);

    public static TheoryData<string, Dictionary<string, JsonNode?>, string> InvalidCapabilities => new()
    {
        { "user and accessKey", new() { ["user"] = "mallory", ["accessKey"] = "spoofed" }, "`Capabilities` cannot set `user`, `accessKey`" },
        { "name", new() { ["name"] = "smoke" }, "`Capabilities` cannot set `name`" },
        { "nested LT:Options", new() { ["LT:Options"] = new JsonObject { ["video"] = true } }, "pass `LT:Options` fields directly in `Capabilities`" },
        { "browserName", new() { ["browserName"] = "Firefox" }, "set `browserName` as an option" },
        { "browserOptions as a string", new() { ["browserOptions"] = "--no-sandbox" }, "`Capabilities[\"browserOptions\"]` must be an array of strings" },
        { "browserOptions with a number", new() { ["browserOptions"] = new JsonArray(1) }, "`Capabilities[\"browserOptions\"]` must be an array of strings" },
        { "browserOptions null", new() { ["browserOptions"] = null }, "`Capabilities[\"browserOptions\"]` must be an array of strings" },
        { "custom object with NaN", new() { ["geoLocation"] = JsonValue.Create(new Wrapped(double.NaN)) }, "`Capabilities[\"geoLocation\"]` cannot be encoded as JSON" },
        { "idleTimeout NaN", new() { ["idleTimeout"] = double.NaN }, "`Capabilities[\"idleTimeout\"]` contains a non-finite number" },
        { "nested infinity", new() { ["network"] = new JsonObject { ["rules"] = new JsonArray(double.PositiveInfinity) } }, "`Capabilities[\"network\"].rules[0]` contains a non-finite number" },
    };

    /// <summary>
    /// Partial: ports the rows of upstream's <c>it.each</c> for <c>user</c>/<c>accessKey</c>, <c>name</c>, a nested
    /// <c>LT:Options</c>, <c>browserName</c> in <c>capabilities</c>, <c>browserOptions</c> as a string, as <c>[1]</c> and as null, a value JSON cannot encode, and a
    /// non-finite <c>idleTimeout</c>. The other rows have no C# form: unknown option keys, a <c>browserName</c> check,
    /// non-object <c>capabilities</c>, and a Date, Map or function value.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidCapabilities))]
    public void Partial_Rejects_options_when_the_provider_is_created(string row, Dictionary<string, JsonNode?> capabilities, string message)
    {
        Assert.NotEmpty(row);

        var error = Assert.Throws<ConfigurationException>(() => new TestMuBrowsers(new TestMuBrowsersOptions { Capabilities = capabilities }));

        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }
}
