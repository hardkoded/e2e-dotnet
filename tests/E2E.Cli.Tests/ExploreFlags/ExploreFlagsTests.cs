// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Tests.Helpers;

namespace E2E.Cli.Tests.ExploreFlags;

/// <summary>
/// .NET-only: the flags of <c>e2e explore</c>, its exit codes, and the accounts it reads from the environment. Upstream's
/// commander parses its flags and its tests cover <c>run</c>'s; the port parses its own, so these have no upstream <c>it</c>.
/// </summary>
[Collection(ExploreEnvironment.Name)]
public sealed class ExploreFlagsTests
{
    private static readonly ExploreBudgets Budgets = new(4, TimeSpan.FromMinutes(5));

    private static ExploreOutcome Passed() =>
        new(RunStatus.Passed, 0, null, new ReportExplore("Explore the app and find bugs", Budgets, ReportExploreEnded.Finished, [], []));

    private static FakeExplore Fake(ExploreOutcome? outcome = null) => new([], outcome ?? Passed());

    [Fact]
    public async Task Passes_every_flag_and_the_goal_to_the_exploration()
    {
        var fake = Fake();
        var (code, _, _) = await fake.InvokeAsync(
            "Explore checkout", "--config", "cfg/e2e.config.json", "--target=web", "--agent", "ux", "--max-steps", "4", "--timeout=240000", "--headed", "--output", "out");

        Assert.Equal(0, code);
        var options = Assert.IsType<ExploreOptions>(fake.Options);
        Assert.Equal("Explore checkout", options.Goal);
        Assert.Equal("cfg/e2e.config.json", options.ConfigPath);
        Assert.Equal("web", options.Target);
        Assert.Equal("ux", options.Agent);
        Assert.Equal(4, options.MaxSteps);
        Assert.Equal(TimeSpan.FromMinutes(4), options.Timeout);
        Assert.True(options.Headed);
        Assert.Equal("out", options.Output);
    }

    [Fact]
    public async Task Leaves_everything_to_the_defaults_with_no_flags()
    {
        var fake = Fake();
        var (code, _, _) = await fake.InvokeAsync();

        Assert.Equal(0, code);
        var options = Assert.IsType<ExploreOptions>(fake.Options);
        Assert.Null(options.Goal);
        Assert.Null(options.MaxSteps);
        Assert.Null(options.Timeout);
        Assert.False(options.Headed);
    }

    [Theory]
    [InlineData("--max-steps", "0", "option '--max-steps <n>' argument '0' is invalid. must be an integer from 1 through 12")]
    [InlineData("--max-steps", "13", "option '--max-steps <n>' argument '13' is invalid. must be an integer from 1 through 12")]
    [InlineData("--max-steps", "two", "option '--max-steps <n>' argument 'two' is invalid. must be an integer from 1 through 12")]
    [InlineData("--timeout", "179999", "option '--timeout <ms>' argument '179999' is invalid. must be an integer from 180000 through 900000")]
    [InlineData("--timeout", "900001", "option '--timeout <ms>' argument '900001' is invalid. must be an integer from 180000 through 900000")]
    [InlineData("--target", null, "option '--target <id>' argument missing")]
    [InlineData("--headed=yes", null, "option '--headed' takes no argument")]
    [InlineData("--nope", null, "unknown option '--nope'")]
    public async Task Refuses_a_bad_flag_with_exit_code_2_and_never_explores(string flag, string? value, string message)
    {
        var fake = Fake();
        var (code, stdout, stderr) = await fake.InvokeAsync(value is null ? [flag] : [flag, value]);

        Assert.Equal(2, code);
        Assert.Null(fake.Options);
        Assert.Equal("", stdout);
        Assert.Equal("error: " + message + "\n", stderr.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Refuses_a_second_goal()
    {
        var fake = Fake();
        var (code, _, stderr) = await fake.InvokeAsync("one", "two");

        Assert.Equal(2, code);
        Assert.Null(fake.Options);
        Assert.Contains("too many arguments for 'explore'. Expected 1 argument but got 2.", stderr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--session")]
    [InlineData("--reporter")]
    public async Task Refuses_the_flags_the_port_has_no_feature_for(string flag)
    {
        var fake = Fake();
        var (code, _, stderr) = await fake.InvokeAsync(flag, "x");

        Assert.Equal(2, code);
        Assert.Null(fake.Options);
        Assert.Contains("option '" + flag + "' is not supported by the .NET port", stderr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--trace")]
    [InlineData("--video")]
    [InlineData("--debug")]
    [InlineData("--ai-trace")]
    public async Task Says_it_ignores_a_recording_or_debug_flag_and_explores_on(string flag)
    {
        var fake = Fake();
        var (code, _, stderr) = await fake.InvokeAsync(flag);

        Assert.Equal(0, code);
        Assert.NotNull(fake.Options);
        Assert.Contains("e2e explore: " + flag + " is not supported by the .NET port; ignored", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prints_the_usage_for_help_without_exploring()
    {
        var fake = Fake();
        var (code, stdout, _) = await fake.InvokeAsync("--max-steps", "4", "--help");

        Assert.Equal(0, code);
        Assert.Null(fake.Options);
        Assert.StartsWith("Usage: e2e explore [options] [goal]", stdout, StringComparison.Ordinal);
        Assert.Contains("exploration steps at most, 1 through 12 (default: 8)", stdout, StringComparison.Ordinal);
        Assert.Contains("wall clock in milliseconds, 180000 through 900000 (default: 600000)", stdout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Exits_with_the_outcomes_code(int exitCode)
    {
        var outcome = new ExploreOutcome(RunStatus.Failed, exitCode, new TestException("TEST_FAILED", "no"), Passed().Explore);
        var (code, _, _) = await Fake(outcome).InvokeAsync();

        Assert.Equal(exitCode, code);
    }

    [Fact]
    public async Task Prints_an_error_thrown_before_the_run_with_its_code_and_the_exit_code_of_its_class()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = await ExploreCommand.RunAsync(
            [],
            stdout,
            stderr,
            interactive: false,
            (_, _) => throw new ConfigurationException("INVALID_CONFIG", "unknown agent \"x\""),
            CancellationToken.None);

        Assert.Equal(2, code);
        Assert.Equal("error INVALID_CONFIG: unknown agent \"x\"", stderr.ToString().TrimEnd());
        Assert.Equal("", stdout.ToString());
    }

    [Fact]
    public async Task Exits_130_when_interrupted()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var code = await ExploreCommand.RunAsync(
            [],
            new StringWriter(),
            new StringWriter(),
            interactive: false,
            (_, token) => Task.FromCanceled<ExploreOutcome>(token),
            cancel.Token);

        Assert.Equal(130, code);
    }

    [Fact]
    public async Task Runs_the_real_exploration_from_the_program_and_fails_a_missing_config_with_exit_code_2()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "e2e.config.json");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var code = await Program.RunAsync(["explore", "--config", missing], stdout, stderr, TextReader.Null, interactive: false, CancellationToken.None);

        Assert.Equal(2, code);
        Assert.Contains("error CONFIG_NOT_FOUND: no config file at " + missing, stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gives_the_agent_each_account_in_the_environment_and_never_prints_a_password()
    {
        const string Password = "hunter2-secret-pw";
        using var _ = Env("E2E_USER_EXPLORE_TEST_USERNAME", "alice@example.com");
        using var __ = Env("E2E_USER_EXPLORE_TEST_PASSWORD", Password);
        var outcome = new ExploreOutcome(
            RunStatus.Failed,
            3,
            new AgentException("MODEL_PROVIDER_FAILED", "the provider echoed " + Password),
            Passed().Explore);
        var fake = Fake(outcome);

        var (_, stdout, stderr) = await fake.InvokeAsync();

        var account = Assert.Contains("explore_test", fake.Options!.Credentials!);
        Assert.Equal("alice@example.com", account.Username);
        Assert.DoesNotContain(Password, stdout + stderr, StringComparison.Ordinal);
        Assert.Contains("MODEL_PROVIDER_FAILED: the provider echoed <secret:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_an_account_without_a_password_with_exit_code_2()
    {
        using var _ = Env("E2E_USER_EXPLORE_HALF_USERNAME", "bob@example.com");
        var fake = Fake();

        var (code, _, stderr) = await fake.InvokeAsync();

        Assert.Equal(2, code);
        Assert.Null(fake.Options);
        Assert.Contains("error AUTH_CREDENTIAL_UNAVAILABLE: Credential 'explore_half' is not configured.", stderr, StringComparison.Ordinal);
    }

    private static EnvScope Env(string name, string value) => new(name, value);

    private sealed class EnvScope : IDisposable
    {
        private readonly string _name;

        public EnvScope(string name, string value)
        {
            _name = name;
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, null);
    }
}
