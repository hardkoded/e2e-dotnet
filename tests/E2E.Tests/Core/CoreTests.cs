// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Tests.Core;

/// <summary>
/// Upstream's <c>describe('e2e/core')</c> (<c>tests/integration/core.test.ts</c>): hosts, sessions, saved state, secrets, timeouts, link bases, and the
/// agent's cache. The <c>it</c> that need a feature the port has not ported (<c>createAgent</c>) are not here, or are partial.
/// </summary>
public sealed class CoreTests : IAsyncLifetime
{
    private static readonly LaunchApp FakeApp = new() { Url = "http://127.0.0.1:4500" };

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-core-tests-").FullName;

    private readonly List<Host> _hosts = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var opened in _hosts)
        {
            try
            {
                await opened.CloseAsync();
            }
            catch (AggregateException)
            {
                // Closing is checked by the tests that look at it.
            }
        }

        Directory.Delete(_dir, recursive: true);
    }

    private static List<SemanticNode> Password() => [new SemanticNode { Ref = "password", Role = "textbox", Name = "Password", Text = "" }];

    private async Task<Host> HostOf(CoreFakeEngine engine, LaunchOptions? options = null)
    {
        var opened = await Launch.LaunchAsync(engine, (options ?? new LaunchOptions()) with { Cwd = _dir, Env = new Dictionary<string, string?>() });
        _hosts.Add(opened);
        return opened;
    }

    private static Task<ConfigurationException> Mismatch(Host target, SessionState state) =>
        Assert.ThrowsAsync<ConfigurationException>(() => target.NewSessionAsync(new NewSessionOptions { State = state }));

    // Partial: the port's session has no fixtures beyond screen, app, platform, and browser (no `gadget` and no key listing), and no `agent` on it.
    [Fact]
    public async Task Partial_Opens_sessions_one_at_a_time_on_one_engine_instance_each_with_screen_app_and_platform()
    {
        var fake = new CoreFakeEngine();
        var opened = await HostOf(fake, new LaunchOptions { App = FakeApp });

        Assert.Equal("fake", opened.Platform);
        var first = await opened.NewSessionAsync();
        await first.App.OpenAsync("/");
        Assert.Equal("fake", first.Platform);
        Assert.NotNull(first.Screen);
        Assert.Equal("WORKER_PROTOCOL", (await Assert.ThrowsAsync<EngineException>(() => opened.NewSessionAsync())).Code);
        await first.CloseAsync();
        await first.CloseAsync();
        var second = await opened.NewSessionAsync();
        await second.CloseAsync();
        await opened.CloseAsync();

        Assert.Equal(2, fake.Starts);
        Assert.Equal(2, fake.Disposes);
        Assert.Equal("WORKER_PROTOCOL", (await Assert.ThrowsAsync<EngineException>(() => opened.NewSessionAsync())).Code);
    }

    [Fact]
    public async Task Refuses_a_second_host_on_the_engine_instance_another_host_owns()
    {
        var fake = new CoreFakeEngine();
        await HostOf(fake);

        Assert.Equal("WORKER_PROTOCOL", (await Assert.ThrowsAsync<EngineException>(() => HostOf(fake))).Code);
    }

    // Partial: C# types an engine and an app, so a non-engine and an app key the config does not take cannot be passed. Checked: a missing engine,
    // and a relative ArtifactsDir resolving from Cwd.
    [Fact]
    public async Task Partial_Refuses_what_is_not_an_engine_and_a_relative_artifactsDir_resolves_from_cwd()
    {
        var missing = await Assert.ThrowsAsync<TestException>(() => Launch.LaunchAsync(null!));
        Assert.Equal("INVALID_ARGUMENT", missing.Code);
        Assert.Contains("launch takes an engine", missing.Message, StringComparison.Ordinal);

        var opened = await HostOf(new CoreFakeEngine(), new LaunchOptions { App = FakeApp, ArtifactsDir = "shots" });
        var session = await opened.NewSessionAsync();
        await session.App.OpenAsync("/");

        Assert.Contains(Path.Combine(_dir, "shots"), await session.App.ScreenshotAsync("product"), StringComparison.Ordinal);
        await session.CloseAsync();
    }

    // Partial: the port keeps no "the session is closed" error for an operation on a closed session. Checked: the error an operation throws is
    // classified and carries no secret.
    [Fact]
    public async Task Partial_Redacts_a_secret_from_the_error_an_operation_throws()
    {
        var fake = new CoreFakeEngine { OnNavigate = url => url.EndsWith("/broken", StringComparison.Ordinal) ? new InvalidOperationException("the page printed hunter2-secret and stopped") : null };
        var opened = await HostOf(fake, new LaunchOptions { App = FakeApp, Secrets = new Dictionary<string, string> { ["SHOP_PASSWORD"] = "hunter2-secret" } });
        var session = await opened.NewSessionAsync();

        var failure = await Assert.ThrowsAnyAsync<E2EException>(() => session.App.OpenAsync("/broken"));

        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.Contains("the page printed", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2-secret", failure.ToString(), StringComparison.Ordinal);
        await session.CloseAsync();
    }

    [Fact]
    public async Task Saves_state_in_memory_exports_it_only_encrypted_and_restores_it_on_a_host_of_the_same_engine_and_app()
    {
        var fake = new CoreFakeEngine(state: true);
        var opened = await HostOf(fake, new LaunchOptions { App = FakeApp });
        var first = await opened.NewSessionAsync();
        var state = await first.SaveStateAsync();
        await first.CloseAsync();
        Assert.Equal(("fake", "fake"), (state.Engine, state.Platform));

        var key = new SessionStateKeyOptions { Key = [.. Enumerable.Repeat((byte)7, 32)] };
        var bytes = await state.ExportAsync(key);
        var document = JsonNode.Parse(Encoding.UTF8.GetString(bytes))!;
        Assert.Equal("session-state-1", (string?)document["schemaVersion"]);
        Assert.DoesNotContain("\"ok\"", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        await Assert.ThrowsAsync<ConfigurationException>(() => SessionState.ImportAsync(bytes, new SessionStateKeyOptions { Key = new byte[32] }));
        Assert.Equal("INVALID_ARGUMENT", (await Assert.ThrowsAsync<TestException>(() => state.ExportAsync(new SessionStateKeyOptions { Key = new byte[16] }))).Code);

        var restored = await SessionState.ImportAsync(bytes, key);
        var second = await opened.NewSessionAsync(new NewSessionOptions { State = restored });
        await second.CloseAsync();
        var applied = Assert.Single(fake.RestoredStates);
        Assert.Equal(("fake-state", 1, """{"ok":true}"""), (applied.Format, applied.Version, applied.Data!.ToJsonString()));

        var elsewhere = await HostOf(new CoreFakeEngine(state: true), new LaunchOptions { App = new LaunchApp { Url = "http://127.0.0.1:4600" } });
        // The state itself names the app it was saved for; an imported one holds its digest alone.
        var named = await Mismatch(elsewhere, state);
        Assert.Equal("SESSION_MISMATCH", named.Code);
        Assert.Matches(@"saved for http://\S+, and this host drives http://127\.0\.0\.1:4600.*app identity", named.Message);
        var imported = await Mismatch(elsewhere, restored);
        Assert.Equal("SESSION_MISMATCH", imported.Code);
        Assert.Contains("saved for another app", imported.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Carries_every_secret_value_a_saved_session_knew_so_a_host_launched_without_them_still_redacts_what_the_app_echoes()
    {
        var saving = await HostOf(new CoreFakeEngine(Password(), state: true), new LaunchOptions { App = FakeApp, Secrets = new Dictionary<string, string> { ["SHOP_PASSWORD"] = "hunter2-secret" } });
        var first = await saving.NewSessionAsync();
        await first.App.OpenAsync("/");
        await first.Screen.GetByRole("textbox", "Password").FillAsync(saving.Secret("SHOP_PASSWORD"));
        var key = new SessionStateKeyOptions { Key = [.. Enumerable.Repeat((byte)3, 32)] };
        var bytes = await (await first.SaveStateAsync()).ExportAsync(key);
        await first.CloseAsync();
        Assert.Equal(["SHOP_PASSWORD"], JsonNode.Parse(Encoding.UTF8.GetString(bytes))!["secrecy"]!["names"]!.AsArray().Select(name => (string?)name));

        var echo = new CoreFakeEngine([new SemanticNode { Ref = "echo", Role = "paragraph", Name = "", Text = "remembered hunter2-secret" }], state: true);
        var restoring = await HostOf(echo, new LaunchOptions { App = FakeApp });
        var second = await restoring.NewSessionAsync(new NewSessionOptions { State = await SessionState.ImportAsync(bytes, key) });
        await second.App.OpenAsync("/");

        var tree = await second.Screen.TreeAsync();
        Assert.Contains("remembered", tree, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2-secret", tree, StringComparison.Ordinal);
        await second.CloseAsync();
    }

    [Fact]
    public async Task Fails_a_capture_with_the_error_classified_and_redacted_carrying_no_engine_cause()
    {
        var fake = new CoreFakeEngine(state: true) { OnCapture = () => new InvalidOperationException("capture failed with capture-secret-123") };
        var opened = await HostOf(fake, new LaunchOptions { App = FakeApp, Secrets = new Dictionary<string, string> { ["CAPTURE"] = "capture-secret-123" } });
        var session = await opened.NewSessionAsync();

        var failure = await Assert.ThrowsAnyAsync<E2EException>(() => session.SaveStateAsync());

        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.DoesNotContain("capture-secret-123", failure.Message, StringComparison.Ordinal);
        Assert.Null(failure.InnerException);
        await session.CloseAsync();
    }

    [Fact]
    public async Task Hands_secrets_out_by_name_and_redacts_their_values_from_what_the_screen_reads()
    {
        var fake = new CoreFakeEngine(Password());
        var opened = await HostOf(fake, new LaunchOptions { App = FakeApp, Secrets = new Dictionary<string, string> { ["SHOP_PASSWORD"] = "hunter2-secret" } });
        Assert.Equal("SECRET_UNAVAILABLE", Assert.Throws<ConfigurationException>(() => opened.Secret("NOPE")).Code);
        var session = await opened.NewSessionAsync();
        await session.App.OpenAsync("/");

        await session.Screen.GetByRole("textbox", "Password").FillAsync(opened.Secret("SHOP_PASSWORD"));

        var fill = Assert.IsType<LocatorAction.Fill>(Assert.Single(fake.Performed).Action);
        Assert.Equal(("hunter2-secret", true), (fill.Value, fill.Sensitive));
        Assert.DoesNotContain("hunter2-secret", await session.Screen.TreeAsync(), StringComparison.Ordinal);
        await session.CloseAsync();
    }

    // Partial: upstream also runs an agent step (createAgent, #1676) and checks that its observations take the new default; the port has no createAgent yet.
    [Fact]
    public async Task Partial_Bounds_each_operation_by_its_default_timeout_which_setDefaultTimeout_moves_with_no_session_deadline()
    {
        var fake = new CoreFakeEngine();
        var opened = await HostOf(fake, new LaunchOptions { App = FakeApp, Timeout = TimeSpan.FromSeconds(10) });
        var session = await opened.NewSessionAsync();
        await session.App.OpenAsync("/");
        session.SetDefaultTimeout(TimeSpan.FromMilliseconds(200));

        var started = DateTime.UtcNow;
        var failure = await Assert.ThrowsAsync<TestException>(() => session.Screen.GetByRole("button", "Missing").TapAsync());

        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
        Assert.Equal("INVALID_ARGUMENT", Assert.Throws<TestException>(() => session.SetDefaultTimeout(TimeSpan.Zero)).Code);
        await session.CloseAsync();
    }

    [Fact]
    public async Task Gives_an_app_open_the_long_default_until_a_program_sets_a_timeout_which_then_bounds_it_too()
    {
        var app = new LaunchApp { BundleId = "com.example.shop", LinkBase = "shopapp://" };
        async Task<TimeSpan?> OpenTimeout(CoreFakeEngine fake, Session session)
        {
            var before = fake.Operations.Count;
            await session.App.OpenAsync("/cart");
            return fake.Operations.Skip(before).First(operation => operation.Method == "session.openLink").Timeout;
        }

        var plain = new CoreFakeEngine(device: true, links: true);
        var session = await (await HostOf(plain, new LaunchOptions { App = app })).NewSessionAsync();
        Assert.True(await OpenTimeout(plain, session) > TimeSpan.FromSeconds(60));
        session.SetDefaultTimeout(TimeSpan.FromMilliseconds(1234));
        Assert.True(await OpenTimeout(plain, session) <= TimeSpan.FromMilliseconds(1234));
        await session.CloseAsync();

        var bounded = new CoreFakeEngine(device: true, links: true);
        var launched = await (await HostOf(bounded, new LaunchOptions { App = app, Timeout = TimeSpan.FromSeconds(5) })).NewSessionAsync();
        Assert.True(await OpenTimeout(bounded, launched) <= TimeSpan.FromSeconds(5));
        await launched.CloseAsync();
    }
}
