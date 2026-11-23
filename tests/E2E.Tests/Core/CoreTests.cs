// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;

namespace E2E.Tests.Core;

/// <summary>
/// Upstream's <c>describe('e2e/core')</c> (<c>tests/integration/core.test.ts</c>): hosts, sessions, saved state, secrets, timeouts, link bases, and the
/// agent's cache. The <c>it</c> that need a feature the port has not ported are not here, or are partial; the <c>it</c> of <c>describe('createAgent')</c> are in <c>CreateAgentTests</c>.
/// </summary>
[Collection(E2E.Tests.SessionSecrecy.SharedSecretsCollection.Name)]
public sealed class CoreTests : IAsyncLifetime
{
    private static readonly LaunchApp FakeApp = new() { Url = "http://127.0.0.1:4500" };

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-core-tests-").FullName;

    private readonly List<IAsyncDisposable> _hosts = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var opened in _hosts)
        {
            try
            {
                await opened.DisposeAsync();
            }
            catch (AggregateException)
            {
                // Closing is checked by the tests that look at it.
            }
        }

        Directory.Delete(_dir, recursive: true);
    }

    private static List<SemanticNode> Password() =>
        [
            new SemanticNode
            {
                Ref = "password",
                Role = "textbox",
                Name = "Password",
                Text = "",
            },
        ];

    private async Task<Host> HostOf(CoreFakeEngine engine, LaunchOptions? options = null)
    {
        var opened = await Launch.LaunchAsync(
            (IEngine)engine,
            (options ?? new LaunchOptions()) with
            {
                Cwd = _dir,
                Env = new Dictionary<string, string?>(),
            }
        );
        _hosts.Add(opened);
        return opened;
    }

    private async Task<Host<CoreFakeFixtures>> FixtureHostOf(CoreFakeEngine engine, LaunchOptions? options = null)
    {
        var opened = await Launch.LaunchAsync(
            engine,
            (options ?? new LaunchOptions()) with
            {
                Cwd = _dir,
                Env = new Dictionary<string, string?>(),
            }
        );
        _hosts.Add(opened);
        return opened;
    }

    private static Task<ConfigurationException> Mismatch(Host target, SessionState state) =>
        Assert.ThrowsAsync<ConfigurationException>(() =>
            target.NewSessionAsync(new NewSessionOptions { State = state })
        );

    // The port's session also holds Browser, the web engine's fixture that every port session carries, so it is in the expected members.
    [Fact]
    public async Task Opens_sessions_one_at_a_time_on_one_engine_instance_each_with_screen_app_and_platform()
    {
        var fake = new CoreFakeEngine(
            scene:
            [
                new SemanticNode
                {
                    Ref = "price",
                    Role = "text",
                    Name = "Price",
                    Text = "39,000",
                    TestId = "sale-price",
                },
            ]
        );
        var opened = await FixtureHostOf(fake, new LaunchOptions { App = FakeApp });

        Assert.Equal("fake", opened.Platform);
        var first = await opened.NewSessionAsync();
        await first.App.OpenAsync("/");
        Assert.Equal("39,000", await first.Screen.GetByTestId("sale-price").TextContentAsync());
        Assert.Equal("gadget on fake", first.Fixtures.Gadget);
        var members = typeof(Session<CoreFakeFixtures>)
            .GetProperties()
            .Select(member => member.Name)
            .Concat(
                typeof(Session<CoreFakeFixtures>)
                    .GetMethods()
                    .Where(method => !method.IsSpecialName && method.DeclaringType != typeof(object))
                    .Select(method => method.Name)
            )
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            [
                "App",
                "Browser",
                "CloseAsync",
                "DisposeAsync",
                "Fixtures",
                "Platform",
                "SaveStateAsync",
                "Screen",
                "SetDefaultTimeout",
            ],
            members
        );
        Assert.Null(typeof(Session<CoreFakeFixtures>).GetProperty("Agent"));
        Assert.Equal(
            "WORKER_PROTOCOL",
            (await Assert.ThrowsAsync<EngineException>(() => opened.NewSessionAsync())).Code
        );
        await first.CloseAsync();
        await first.CloseAsync();
        var second = await opened.NewSessionAsync();
        await second.CloseAsync();
        await opened.CloseAsync();

        Assert.Equal(2, fake.Starts);
        Assert.Equal(2, fake.Disposes);
        Assert.Equal(
            "WORKER_PROTOCOL",
            (await Assert.ThrowsAsync<EngineException>(() => opened.NewSessionAsync())).Code
        );
    }

    [Fact]
    public async Task Runs_past_screen_pause_right_away_in_a_program()
    {
        var fake = new CoreFakeEngine(
            scene:
            [
                new SemanticNode
                {
                    Ref = "price",
                    Role = "text",
                    Name = "Price",
                    Text = "39,000",
                    TestId = "sale-price",
                },
            ]
        );
        var opened = await HostOf(fake, new LaunchOptions { App = FakeApp });
        var session = await opened.NewSessionAsync();
        await session.App.OpenAsync("/");
        await session.Screen.PauseAsync();
        Assert.Equal("39,000", await session.Screen.GetByTestId("sale-price").TextContentAsync());
        await session.CloseAsync();
    }

    [Fact]
    public async Task Refuses_a_second_host_on_the_engine_instance_another_host_owns()
    {
        var fake = new CoreFakeEngine();
        await HostOf(fake);

        Assert.Equal("WORKER_PROTOCOL", (await Assert.ThrowsAsync<EngineException>(() => HostOf(fake))).Code);
    }

    [Fact]
    public async Task Hands_the_engine_of_each_host_a_token_of_its_own_and_leaves_the_target_named_as_the_program_wrote_it()
    {
        var fakes = new[] { new CoreFakeEngine(), new CoreFakeEngine(), new CoreFakeEngine() };
        var opened = await Task.WhenAll(fakes.Take(2).Select(fake => HostOf(fake)));
        opened = [.. opened, await HostOf(fakes[2])];
        foreach (var host in opened)
        {
            await (await host.NewSessionAsync()).CloseAsync();
        }

        var ids = fakes.Select(fake => fake.InitInfos[0].HostId).ToList();
        Assert.Equal(3, ids.Distinct().Count());
        foreach (var id in ids)
        {
            Assert.Matches(@"\A[0-9a-f]{24}\z", id);
        }

        Assert.Equal(["fake", "fake", "fake"], fakes.Select(fake => fake.InitInfos[0].TargetName));

        // Prepare, init, and finish of one host see one token.
        var hooks = new CoreFakeEngine();
        var single = await HostOf(hooks);
        await (await single.NewSessionAsync()).CloseAsync();
        await single.CloseAsync();
        var seen = hooks.HostIds;
        Assert.Equal(3, seen.Count);
        Assert.Matches(@"\A[0-9a-f]{24}\z", seen[0]);
        Assert.Single(seen.Distinct());

        // Hosts from two copies of the library share the process and load their own copy of the module.
        var copyIds = await Task.WhenAll(
            Enumerable
                .Range(0, 2)
                .Select(async index =>
                    await IsolatedHost.HostIdFromOwnCopyAsync(
                        Directory.CreateDirectory(Path.Combine(_dir, $"copy-{index}")).FullName
                    )
                )
        );
        Assert.Matches(@"\A[0-9a-f]{24}\z", copyIds[0]);
        Assert.Matches(@"\A[0-9a-f]{24}\z", copyIds[1]);
        Assert.NotEqual(copyIds[0], copyIds[1]);
    }

    [Fact]
    public async Task Names_a_hosts_artifact_folders_after_its_token_so_a_platform_that_is_a_secrets_value_never_reaches_a_path()
    {
        var secret = "private-label";
        var artifactsDir = Path.Combine(_dir, "kept");
        var opened = await HostOf(
            new CoreFakeEngine(platform: secret),
            new LaunchOptions
            {
                Secrets = new Dictionary<string, string> { ["LABEL"] = secret },
                ArtifactsDir = artifactsDir,
            }
        );
        await (await opened.NewSessionAsync()).CloseAsync();
        await opened.CloseAsync();
        var entries = Directory
            .EnumerateFileSystemEntries(artifactsDir, "*", SearchOption.AllDirectories)
            .Select(entry => Path.GetRelativePath(artifactsDir, entry))
            .ToList();
        Assert.Contains(entries, entry => Regex.IsMatch(entry, @"(^|[\\/])core[\\/]host-[0-9a-f]{24}-[^\\/]+$"));
        Assert.DoesNotContain(entries, entry => entry.Contains(secret, StringComparison.Ordinal));
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

        Assert.Contains(
            Path.Combine(_dir, "shots"),
            await session.App.ScreenshotAsync("product"),
            StringComparison.Ordinal
        );
        await session.CloseAsync();
    }

    [Fact]
    public async Task Says_a_closed_session_is_closed_and_redacts_a_secret_from_the_error_an_operation_throws()
    {
        var fake = new CoreFakeEngine(
            scene:
            [
                new SemanticNode
                {
                    Ref = "add",
                    Role = "button",
                    Name = "Add to cart",
                },
                new SemanticNode
                {
                    Ref = "price",
                    Role = "text",
                    Name = "Price",
                    Text = "$9",
                    TestId = "sale-price",
                },
            ]
        )
        {
            OnNavigate = url =>
                url.EndsWith("/broken", StringComparison.Ordinal)
                    ? new InvalidOperationException("the page printed hunter2-secret and stopped")
                    : null,
        };
        var opened = await HostOf(
            fake,
            new LaunchOptions
            {
                App = FakeApp,
                Secrets = new Dictionary<string, string> { ["SHOP_PASSWORD"] = "hunter2-secret" },
            }
        );
        var session = await opened.NewSessionAsync();

        var failure = await Assert.ThrowsAnyAsync<E2EException>(() => session.App.OpenAsync("/broken"));

        // Whatever an engine throws reaches the program with a code.
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.Contains("the page printed", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2-secret", failure.ToString(), StringComparison.Ordinal);
        await session.CloseAsync();

        var tap = await Assert.ThrowsAnyAsync<E2EException>(() =>
            session.Screen.GetByRole("button", "Add to cart").TapAsync()
        );
        Assert.Equal("CANCELLED", tap.Code);
        Assert.Contains("the session is closed", tap.Message, StringComparison.Ordinal);
        Assert.Equal("CANCELLED", (await Assert.ThrowsAnyAsync<E2EException>(() => session.App.OpenAsync("/"))).Code);
        // A read reaches the engine with no step around it, and says the same.
        var count = await Assert.ThrowsAnyAsync<E2EException>(() =>
            session.Screen.GetByTestId("sale-price").CountAsync()
        );
        Assert.Equal("CANCELLED", count.Code);
        Assert.Equal("the session is closed", count.Message);
    }

    [Fact]
    public async Task Cuts_the_calls_still_running_when_a_session_or_its_host_closes_each_by_name_and_closes_cleanly()
    {
        var fake = new CoreFakeEngine(
            scene:
            [
                new SemanticNode
                {
                    Ref = "price",
                    Role = "text",
                    Name = "Price",
                    Text = "$9",
                    TestId = "sale-price",
                },
            ]
        )
        {
            OnNavigateTask = url =>
                url.EndsWith("/slow", StringComparison.Ordinal) ? new TaskCompletionSource().Task : null,
        };
        var opened = await HostOf(fake, new LaunchOptions { App = FakeApp });
        var session = await opened.NewSessionAsync();
        await session.App.OpenAsync("/");
        var missing = session.Screen.GetByRole("button", "Missing");
        static async Task<Exception?> Failed(Task call)
        {
            try
            {
                await call;
                return null;
            }
            catch (Exception cause)
            {
                return cause;
            }
        }

        // A step that returns its own task and one an async method wraps end the same way.
        var tap = Failed(missing.TapAsync());
        var wait = Failed(missing.WaitForAsync());

        // Closing is how a program cancels: what it cut is the caller's to handle, never a failure of the close.
        await session.CloseAsync();

        foreach (var (failure, api) in new[] { (await tap, "locator.tap"), (await wait, "locator.waitFor") })
        {
            var cut = Assert.IsAssignableFrom<E2EException>(failure);
            Assert.Equal("CANCELLED", cut.Code);
            Assert.StartsWith("the session closed while " + api + " \"", cut.Message, StringComparison.Ordinal);
            Assert.EndsWith("\" was running", cut.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(CoreTests), cut.StackTrace, StringComparison.Ordinal);
        }

        var next = await opened.NewSessionAsync();
        var open = Failed(next.App.OpenAsync("/slow"));
        await opened.CloseAsync();
        var opening = Assert.IsAssignableFrom<E2EException>(await open);
        Assert.Equal("CANCELLED", opening.Code);
        Assert.Equal("the session closed while app.open \"/slow\" was running", opening.Message);

        // A call made after the close has nothing to name.
        var after = await Assert.ThrowsAnyAsync<E2EException>(() => next.App.OpenAsync("/"));
        Assert.Equal("CANCELLED", after.Code);
        Assert.Equal("the session is closed", after.Message);
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
        await Assert.ThrowsAsync<ConfigurationException>(() =>
            SessionState.ImportAsync(bytes, new SessionStateKeyOptions { Key = new byte[32] })
        );
        Assert.Equal(
            "INVALID_ARGUMENT",
            (
                await Assert.ThrowsAsync<TestException>(() =>
                    state.ExportAsync(new SessionStateKeyOptions { Key = new byte[16] })
                )
            ).Code
        );

        var restored = await SessionState.ImportAsync(bytes, key);
        var second = await opened.NewSessionAsync(new NewSessionOptions { State = restored });
        await second.CloseAsync();
        var applied = Assert.Single(fake.RestoredStates);
        Assert.Equal(
            ("fake-state", 1, """{"ok":true}"""),
            (applied.Format, applied.Version, applied.Data!.ToJsonString())
        );

        var elsewhere = await HostOf(
            new CoreFakeEngine(state: true),
            new LaunchOptions { App = new LaunchApp { Url = "http://127.0.0.1:4600" } }
        );
        // The state itself names the app it was saved for; an imported one holds its digest alone.
        var named = await Mismatch(elsewhere, state);
        Assert.Equal("SESSION_MISMATCH", named.Code);
        Assert.Matches(
            @"saved for http://\S+, and this host drives http://127\.0\.0\.1:4600.*app identity",
            named.Message
        );
        var imported = await Mismatch(elsewhere, restored);
        Assert.Equal("SESSION_MISMATCH", imported.Code);
        Assert.Contains("saved for another app", imported.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redacts_in_each_session_the_values_every_other_host_was_given_and_carries_none_of_them_in_its_saved_state()
    {
        const string browserSecret = "browser-only-secret-5511";
        const string phoneSecret = "phone-only-secret-8823";
        static CoreFakeEngine Showing(string text, bool state = false) =>
            new(
                [
                    new SemanticNode
                    {
                        Ref = "echo",
                        Role = "paragraph",
                        Name = "",
                        Text = text,
                    },
                ],
                state: state
            );

        var browser = await HostOf(
            Showing($"the phone sent {phoneSecret}", state: true),
            new LaunchOptions
            {
                App = FakeApp,
                Secrets = new Dictionary<string, string> { ["BROWSER"] = browserSecret },
                Timeout = TimeSpan.FromMilliseconds(200),
            }
        );
        var onBrowser = await browser.NewSessionAsync();
        await onBrowser.App.OpenAsync("/");
        Assert.Contains(phoneSecret, await onBrowser.Screen.TreeAsync(), StringComparison.Ordinal);

        // A session opened before the other host redacts its values from the moment it declares them; one opened after does too.
        var phone = await HostOf(
            Showing($"the browser sent {browserSecret}"),
            new LaunchOptions
            {
                App = FakeApp,
                Secrets = new Dictionary<string, string> { ["PHONE"] = phoneSecret },
            }
        );
        Assert.Contains("the phone sent <secret:PHONE>", await onBrowser.Screen.TreeAsync(), StringComparison.Ordinal);
        var onPhone = await phone.NewSessionAsync();
        await onPhone.App.OpenAsync("/");
        Assert.Contains(
            "the browser sent <secret:BROWSER>",
            await onPhone.Screen.TreeAsync(),
            StringComparison.Ordinal
        );

        var failure = await Assert.ThrowsAsync<TestException>(() => onBrowser.Screen.GetByText(phoneSecret).TapAsync());
        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.Contains("<secret:PHONE>", failure.Message, StringComparison.Ordinal);
        Assert.Contains("<secret:PHONE>", failure.Details?.Locator, StringComparison.Ordinal);
        Assert.DoesNotContain(
            phoneSecret,
            $"{failure.Message} {failure.StackTrace} {System.Text.Json.JsonSerializer.Serialize(failure.Details)}",
            StringComparison.Ordinal
        );

        // The browser session redacts the phone's value and does not hold it: its exported state names its own secret alone.
        var exported = Encoding.Latin1.GetString(
            await (await onBrowser.SaveStateAsync()).ExportAsync(
                new SessionStateKeyOptions { Key = [.. Enumerable.Repeat((byte)7, 32)] }
            )
        );
        Assert.Contains("BROWSER", exported, StringComparison.Ordinal);
        Assert.DoesNotContain("PHONE", exported, StringComparison.Ordinal);

        // A program's host shares its values while it is open, and no longer.
        await phone.CloseAsync();
        Assert.Contains(phoneSecret, await onBrowser.Screen.TreeAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Carries_every_secret_value_a_saved_session_knew_so_a_host_launched_without_them_still_redacts_what_the_app_echoes()
    {
        var saving = await HostOf(
            new CoreFakeEngine(Password(), state: true),
            new LaunchOptions
            {
                App = FakeApp,
                Secrets = new Dictionary<string, string> { ["SHOP_PASSWORD"] = "hunter2-secret" },
            }
        );
        var first = await saving.NewSessionAsync();
        await first.App.OpenAsync("/");
        await first.Screen.GetByRole("textbox", "Password").FillAsync(saving.Secret("SHOP_PASSWORD"));
        var key = new SessionStateKeyOptions { Key = [.. Enumerable.Repeat((byte)3, 32)] };
        var bytes = await (await first.SaveStateAsync()).ExportAsync(key);
        await first.CloseAsync();
        Assert.Equal(
            ["SHOP_PASSWORD"],
            JsonNode.Parse(Encoding.UTF8.GetString(bytes))!["secrecy"]!["names"]!
                .AsArray()
                .Select(name => (string?)name)
        );

        var echo = new CoreFakeEngine(
            [
                new SemanticNode
                {
                    Ref = "echo",
                    Role = "paragraph",
                    Name = "",
                    Text = "remembered hunter2-secret",
                },
            ],
            state: true
        );
        var restoring = await HostOf(echo, new LaunchOptions { App = FakeApp });
        var second = await restoring.NewSessionAsync(
            new NewSessionOptions { State = await SessionState.ImportAsync(bytes, key) }
        );
        await second.App.OpenAsync("/");

        var tree = await second.Screen.TreeAsync();
        Assert.Contains("remembered", tree, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2-secret", tree, StringComparison.Ordinal);
        await second.CloseAsync();
    }

    [Fact]
    public async Task Fails_a_capture_with_the_error_classified_and_redacted_carrying_no_engine_cause()
    {
        var fake = new CoreFakeEngine(state: true)
        {
            OnCapture = () => new InvalidOperationException("capture failed with capture-secret-123"),
        };
        var opened = await HostOf(
            fake,
            new LaunchOptions
            {
                App = FakeApp,
                Secrets = new Dictionary<string, string> { ["CAPTURE"] = "capture-secret-123" },
            }
        );
        var session = await opened.NewSessionAsync();

        var failure = await Assert.ThrowsAnyAsync<E2EException>(() => session.SaveStateAsync());

        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.DoesNotContain("capture-secret-123", failure.Message, StringComparison.Ordinal);
        Assert.Null(failure.InnerException);
        await session.CloseAsync();
    }

    private sealed class GuestSeat<TOpened, TTaken>(CancellationToken signal, Func<TOpened, TTaken?> take)
        : IGuestSeat<TOpened, TTaken>
    {
        public CancellationToken Signal { get; } = signal;

        public TTaken? Take(TOpened opened) => take(opened);
    }

    private sealed class SeatsOnlyForTheFirstHost : IAttemptGuests
    {
        private int _seats;

        // The first host is taken, with a session nobody takes; the second host is not taken either.
        private readonly GuestSeat<IGuestHost, bool> _gaveUpHost = new(new CancellationTokenSource().Token, _ => false);
        private readonly GuestSeat<IGuestSession, IGuestSteps> _gaveUpSession = new(
            new CancellationTokenSource().Token,
            _ => null
        );

        public bool Video => false;

        public IGuestSeat<IGuestHost, bool> Host() =>
            Interlocked.Increment(ref _seats) == 1
                ? new GuestSeat<IGuestHost, bool>(new CancellationTokenSource().Token, _ => true)
                : _gaveUpHost;

        public IGuestSeat<IGuestSession, IGuestSteps> Session() => _gaveUpSession;

        public IGuestSteps Visit(string root) => throw new NotSupportedException();

        public void GaveUp(Exception error) { }
    }

    [Fact]
    public async Task Closes_a_host_and_a_session_whose_test_gave_up_its_seat_while_it_was_opening()
    {
        using var ended = new CancellationTokenSource();
        using var published = Attempts.PublishAttempt(
            new PublishedAttempt { AttemptId = "a1", Guests = new SeatsOnlyForTheFirstHost() },
            ended.Token
        );
        try
        {
            var kept = new CoreFakeEngine();
            var opened = await HostOf(kept);
            var failure = await Assert.ThrowsAsync<EngineException>(() => opened.NewSessionAsync());
            Assert.Equal("CANCELLED", failure.Code);
            Assert.Contains("ended before it was open", failure.Message, StringComparison.Ordinal);
            var started = kept.Stats();
            Assert.Equal((1, 1), (started.AttemptsStarted, started.AttemptsEnded));

            var dropped = new CoreFakeEngine();
            var dropping = await Assert.ThrowsAsync<EngineException>(() =>
                Launch.LaunchAsync(
                    (IEngine)dropped,
                    new LaunchOptions
                    {
                        App = FakeApp,
                        Cwd = _dir,
                        Env = new Dictionary<string, string?>(),
                    }
                )
            );
            Assert.Equal("CANCELLED", dropping.Code);
            Assert.Contains("ended before it was open", dropping.Message, StringComparison.Ordinal);
            var finished = dropped.Stats();
            Assert.Equal((1, 1), (finished.Inits, finished.Disposes));
        }
        finally
        {
            await ended.CancelAsync();
        }
    }

    [Fact]
    public async Task Hands_secrets_out_by_name_and_redacts_their_values_from_what_the_screen_reads()
    {
        var fake = new CoreFakeEngine(Password());
        var opened = await HostOf(
            fake,
            new LaunchOptions
            {
                App = FakeApp,
                Secrets = new Dictionary<string, string> { ["SHOP_PASSWORD"] = "hunter2-secret" },
            }
        );
        Assert.Equal("SECRET_UNAVAILABLE", Assert.Throws<ConfigurationException>(() => opened.Secret("NOPE")).Code);
        var session = await opened.NewSessionAsync();
        await session.App.OpenAsync("/");

        await session.Screen.GetByRole("textbox", "Password").FillAsync(opened.Secret("SHOP_PASSWORD"));

        var fill = Assert.IsType<LocatorAction.Fill>(Assert.Single(fake.Performed).Action);
        Assert.Equal(("hunter2-secret", true), (fill.Value, fill.Sensitive));
        Assert.DoesNotContain("hunter2-secret", await session.Screen.TreeAsync(), StringComparison.Ordinal);
        await session.CloseAsync();
    }

    [Fact]
    public async Task Bounds_each_operation_by_its_default_timeout_which_setDefaultTimeout_moves_with_no_session_deadline()
    {
        var fake = new CoreFakeEngine();
        var opened = await HostOf(fake, new LaunchOptions { App = FakeApp, Timeout = TimeSpan.FromSeconds(10) });
        var session = await opened.NewSessionAsync();
        await session.App.OpenAsync("/");
        session.SetDefaultTimeout(TimeSpan.FromMilliseconds(200));

        var started = DateTime.UtcNow;
        var failure = await Assert.ThrowsAsync<TestException>(() =>
            session.Screen.GetByRole("button", "Missing").TapAsync()
        );

        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
        Assert.Equal(
            "INVALID_ARGUMENT",
            Assert.Throws<TestException>(() => session.SetDefaultTimeout(TimeSpan.Zero)).Code
        );

        // The agent's engine operations take the same default.
        session.SetDefaultTimeout(TimeSpan.FromMilliseconds(1_234));
        var before = fake.Operations.Count;
        var observer = new TestExecutor("observer", async (context, _) =>
        {
            await context.ObserveAsync();
            return ExecutorKit.Passed("looked");
        }) { Version = "1" };
        await CoreAgents
            .CreateAgent(session, new CreateAgentOptions { Executor = observer })
            .ActAsync("look at the page");
        var observes = fake.Operations.Skip(before).Where(operation => operation.Method == "observe").ToList();
        Assert.NotEmpty(observes);
        foreach (var operation in observes)
        {
            Assert.True(operation.Timeout <= TimeSpan.FromMilliseconds(1_234));
        }

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
        var launched = await (
            await HostOf(bounded, new LaunchOptions { App = app, Timeout = TimeSpan.FromSeconds(5) })
        ).NewSessionAsync();
        Assert.True(await OpenTimeout(bounded, launched) <= TimeSpan.FromSeconds(5));
        await launched.CloseAsync();
    }
}
