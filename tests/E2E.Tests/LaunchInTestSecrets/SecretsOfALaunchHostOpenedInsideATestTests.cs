// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Tests.Core;
using R = E2E.Reports;

namespace E2E.Tests.LaunchInTestSecrets;

/// <summary>
/// Upstream: packages/e2e/tests/integration/launch-in-test-secrets.test.ts, describe('secrets of a launch() host opened inside a test'): a test that
/// opens a second session with <c>Launch.LaunchAsync</c>. The run and the host each know secrets the other was never given, and neither prints the
/// other's, a value the test's own session resolved from a provider included.
/// </summary>
[Collection(E2E.Tests.SessionSecrecy.SharedSecretsCollection.Name)]
public sealed class SecretsOfALaunchHostOpenedInsideATestTests : IAsyncLifetime
{
    private const string HostValue = "host-only-secret-7731";
    private const string RunValue = "run-only-secret-4096";
    private const string CodeValue = "one-time-code-558201";

    private const string Shown = "the test session shows a value only the host was given";
    private const string Asserted =
        "an assertion fails on a value read from the host session, in a test that never uses its own";
    private const string Named = "the test session names a locator with the value";
    private const string AfterProvider =
        "the host session, opened after the test session resolved a provider value, redacts it";
    private const string Declared = "the host session redacts what the run declared";

    private static readonly LaunchApp FakeApp = new() { Url = "http://127.0.0.1:4500" };

    private readonly List<IAsyncDisposable> _hosts = [];
    private RunOutcome _outcome = null!;
    private FixtureProject _project = null!;

    // Upstream's run config holds `CODE: () => value`; the config JSON holds no function, so the test's own handle resolves through this provider.
    private static readonly SecretProvider CodeProvider = _ => Task.FromResult(CodeValue);

    private static SemanticNode Node(
        string reference,
        string role,
        string name,
        string text = "",
        string? value = null
    ) =>
        new()
        {
            Ref = reference,
            Role = role,
            Name = name,
            Text = text,
            Value = value,
        };

    // The engine of the second session: the run's value is echoed, the code arrives by SMS, and a field takes what is filled into it.
    private static CoreFakeEngine Second() =>
        new(
            [
                Node("echo", "paragraph", "", "the run sent " + RunValue),
                Node("sms", "paragraph", "", "your code is " + CodeValue),
                Node("code", "textbox", "Code", value: ""),
            ],
            fills: true
        );

    private async Task<Host> Launched(IReadOnlyDictionary<string, string>? secrets = null, TimeSpan? timeout = null)
    {
        var host = await Launch.LaunchAsync(
            (IEngine)Second(),
            new LaunchOptions
            {
                App = FakeApp,
                Secrets = secrets,
                Timeout = timeout,
                Env = new Dictionary<string, string?>(),
            }
        );
        lock (_hosts)
        {
            _hosts.Add(host);
        }

        return host;
    }

    private IReadOnlyList<InMemoryTest> Suite()
    {
        var invite = new Dictionary<string, string> { ["INVITE"] = HostValue };
        var code = E2EConfig
            .Parse("""{ "secrets": {} }""", _project.Dir, _ => null)
            .WithSecretProvider("CODE", CodeProvider)
            .Secrets.Get("CODE");

        return [new InMemoryTest(Shown, async session =>
            {
                await session.App.OpenAsync();
                var host = await Launched(invite);
                // Closed before the failure: the screen still shows the value, and the test that launched the host still redacts it.
                await host.CloseAsync();
                await Expect.That(session.Screen.GetByText("never on the screen")).ToBeVisibleAsync();
            }), new InMemoryTest(Asserted, async _ =>
            {
                var host = await Launched(invite);
                try
                {
                    var second = await host.NewSessionAsync();
                    await second.App.OpenAsync("/");
                    await second.Screen.GetByLabel("Code").FillAsync(host.Secret("INVITE"));
                    Expect.That(await second.Screen.GetByLabel("Code").InputValueAsync()).ToBe("another value");
                }
                finally
                {
                    await host.CloseAsync();
                }
            }), new InMemoryTest(Named, async session =>
            {
                await session.App.OpenAsync();
                var host = await Launched(invite);
                try
                {
                    await session.Screen.GetByText(HostValue).TapAsync();
                }
                finally
                {
                    await host.CloseAsync();
                }
            }), new InMemoryTest(AfterProvider, async session =>
            {
                await session.App.OpenAsync();
                await session.Screen.GetByLabel("Code").FillAsync(code);
                var host = await Launched(invite, TimeSpan.FromMilliseconds(300));
                try
                {
                    var second = await host.NewSessionAsync();
                    await second.App.OpenAsync("/");
                    Assert.Contains(
                        "your code is <secret:CODE>",
                        await second.Screen.TreeAsync(),
                        StringComparison.Ordinal
                    );
                    // Fails with the value in its label and on the session's screen: both reach the test's report through the session.
                    await second.Screen.GetByRole("button", CodeValue).TapAsync();
                }
                finally
                {
                    await host.CloseAsync();
                }
            }), new InMemoryTest(Declared, async session =>
            {
                await session.App.OpenAsync();
                var host = await Launched();
                try
                {
                    var second = await host.NewSessionAsync();
                    await second.App.OpenAsync("/");
                    Assert.Contains(
                        "the run sent <secret:RUN_ONLY>",
                        await second.Screen.TreeAsync(),
                        StringComparison.Ordinal
                    );
                }
                finally
                {
                    await host.CloseAsync();
                }
            })];
    }

    public async Task InitializeAsync()
    {
        // The run registers its secrets for the process, so the process starts the file with none shared.
        E2E.Tests.SessionSecrecy.SecretsSharedBySessionsInOneProcessTests.ClearShared();
        _project = RunProject.CreateProject();
        var config = JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                ["secrets"] = new Dictionary<string, string> { ["RUN_ONLY"] = RunValue },
                ["trace"] = "on",
                ["actionTimeout"] = 300,
                ["assertionTimeout"] = 200,
            }
        );
        _outcome = await RunProject.RunExistingAsync(
            _project,
            new RunProjectOptions
            {
                Tests = Suite(),
                Engine = new CoreFakeEngine(
                    [
                        Node("echo", "paragraph", "", "the host sent " + HostValue),
                        Node("code", "textbox", "Code", value: ""),
                    ],
                    fills: true
                ),
                Config = config,
            }
        );
    }

    public async Task DisposeAsync()
    {
        try
        {
            foreach (var host in _hosts)
            {
                await host.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                _project.Cleanup();
            }
            finally
            {
                E2E.Tests.SessionSecrecy.SecretsSharedBySessionsInOneProcessTests.ClearShared();
            }
        }
    }

    private R.ReportAttempt AttemptOf(string title) => RunProject.ResultByTitle(_outcome, title).Attempts[0];

    private string Written => Path.Combine(_project.Dir, ".e2e");

    [Fact]
    public void Fails_the_three_tests_for_their_own_reasons()
    {
        Assert.Equal("ASSERTION_FAILED", AttemptOf(Shown).Error?.Code);
        var asserted = AttemptOf(Asserted).Error;
        Assert.Equal("ASSERTION_FAILED", asserted?.Code);
        Assert.Contains("<secret:INVITE>", asserted!.Message, StringComparison.Ordinal);
        var named = AttemptOf(Named).Error;
        Assert.Equal("LOCATOR_NOT_FOUND", named?.Code);
        Assert.Contains("<secret:INVITE>", named!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_hosts_value_out_of_the_report_and_of_every_file_the_run_wrote()
    {
        Assert.DoesNotContain(HostValue, JsonSerializer.Serialize(_outcome.Report), StringComparison.Ordinal);
        var written = RunProject.ContentsUnder(Written);
        Assert.NotEmpty(written);
        foreach (var (file, content) in written)
        {
            Assert.False(content.Contains(HostValue, StringComparison.Ordinal), file);
        }

        // The test session did see the value: its failure evidence names the secret in its place.
        Assert.Contains(written, entry =>
            entry.Content.Contains("the host sent <secret:INVITE>", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Redacts_a_value_the_test_session_resolved_from_a_provider_in_a_host_session_opened_after_it_and_in_what_that_session_reports()
    {
        var attempt = AttemptOf(AfterProvider);
        Assert.Equal("LOCATOR_NOT_FOUND", attempt.Error?.Code);
        Assert.Contains("<secret:CODE>", attempt.Error!.Message, StringComparison.Ordinal);
        var last = attempt.Steps[^1];
        Assert.Equal("launch-1", last.Actor);
        Assert.Contains("<secret:CODE>", last.Label, StringComparison.Ordinal);
        Assert.DoesNotContain(CodeValue, JsonSerializer.Serialize(_outcome.Report), StringComparison.Ordinal);
        foreach (var (file, content) in RunProject.ContentsUnder(Written))
        {
            Assert.False(content.Contains(CodeValue, StringComparison.Ordinal), file);
        }
    }

    [Fact]
    public void Redacts_the_runs_value_in_the_host_session()
    {
        var result = RunProject.ResultByTitle(_outcome, Declared);
        Assert.True(result.Status == R.ResultStatus.Passed, JsonSerializer.Serialize(result.Attempts[0].Error));
    }
}
