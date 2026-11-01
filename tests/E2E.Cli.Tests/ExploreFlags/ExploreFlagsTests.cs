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
        Assert.Equal("error: " + message + "\n", stderr);
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

    [Fact]
    public async Task Passes_the_debug_and_ai_trace_switches_and_the_trace_and_video_modes_to_the_exploration()
    {
        var fake = Fake();
        var (code, _, stderr) = await fake.InvokeAsync("--debug", "--ai-trace", "--trace=on-all-retries", "--video", "retain-on-failure");

        Assert.Equal(0, code);
        Assert.Equal("", stderr);
        var options = Assert.IsType<ExploreOptions>(fake.Options);
        Assert.True(options.Debug);
        Assert.True(options.AiTrace);
        Assert.Equal(RecordingMode.OnAllRetries, options.Trace);
        Assert.Equal(RecordingMode.RetainOnFailure, options.Video);
    }

    [Fact]
    public async Task Reads_a_bare_trace_or_video_flag_as_on_and_leaves_them_unset_without_one()
    {
        var bare = Fake();
        await bare.InvokeAsync("--trace", "--video");
        Assert.Equal(RecordingMode.On, bare.Options!.Trace);
        Assert.Equal(RecordingMode.On, bare.Options.Video);

        var none = Fake();
        await none.InvokeAsync("Explore checkout");
        Assert.False(none.Options!.Debug);
        Assert.False(none.Options.AiTrace);
        Assert.Null(none.Options.Trace);
        Assert.Null(none.Options.Video);

        // Another option after the flag is not its value.
        var before = Fake();
        await before.InvokeAsync("--video", "--headed", "Explore checkout");
        Assert.Equal(RecordingMode.On, before.Options!.Video);
        Assert.True(before.Options.Headed);
        Assert.Equal("Explore checkout", before.Options.Goal);
    }

    [Theory]
    [InlineData("--trace", "Explore checkout", "write --trace=<mode>, or put test files before --trace")]
    [InlineData("--video=sometimes", null, "expected a mode (off, on, retain-on-failure, on-first-retry, on-all-retries), got \"sometimes\"")]
    [InlineData("--trace=all", null, "\"all\" is the old spelling of --trace on; the modes are off, on, retain-on-failure, on-first-retry, on-all-retries")]
    [InlineData("--trace=retries", null, "\"retries\" is the old spelling of --trace on-all-retries")]
    public async Task Refuses_a_trace_or_video_value_that_is_not_a_mode_with_the_way_to_write_one(string flag, string? next, string message)
    {
        var fake = Fake();
        var (code, _, stderr) = await fake.InvokeAsync(next is null ? [flag] : [flag, next]);

        Assert.Equal(2, code);
        Assert.Null(fake.Options);
        Assert.Contains(message, stderr, StringComparison.Ordinal);
        Assert.StartsWith("error: option '" + flag.Split('=')[0] + " [mode]' argument '", stderr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--debug=1")]
    [InlineData("--ai-trace=yes")]
    public async Task Refuses_a_value_given_to_a_switch(string flag)
    {
        var fake = Fake();
        var (code, _, stderr) = await fake.InvokeAsync(flag);

        Assert.Equal(2, code);
        Assert.Equal("error: option '" + flag.Split('=')[0] + "' takes no argument\n", stderr);
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
        Assert.Contains("--trace [mode]", stdout, StringComparison.Ordinal);
        Assert.Contains("Modes: off, on, retain-on-failure, on-first-retry, on-all-retries (also E2E_VIDEO)", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Not supported by the .NET port: --session, --reporter (it refuses these), and", stdout, StringComparison.Ordinal);
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
    public async Task Exits_130_when_interrupted_while_the_host_opens()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var stderr = new StringWriter();
        var code = await ExploreCommand.RunAsync(
            [],
            new StringWriter(),
            stderr,
            interactive: false,
            (_, _) => Task.FromException<ExploreOutcome>(new EngineException(EngineErrorCodes.Cancelled, "opening a host was cancelled")),
            cancel.Token);

        Assert.Equal(130, code);
        Assert.Equal("", stderr.ToString());
    }

    [Fact]
    public async Task Keeps_CONFIG_NOT_FOUND_without_a_terminal_and_with_config()
    {
        // The .git directory stops config discovery here, whatever the temp directory's parents hold.
        var dir = Directory.CreateTempSubdirectory("e2e-explore-881-").FullName;
        Directory.CreateDirectory(Path.Combine(dir, ".git"));
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var code = await Program.RunAsync(["explore"], stdout, stderr, TextReader.Null, interactive: false, CancellationToken.None, dir);

            Assert.Equal(2, code);
            Assert.Contains("CONFIG_NOT_FOUND", stderr.ToString(), StringComparison.Ordinal);
            Assert.Contains("found from " + dir + " upward", stderr.ToString(), StringComparison.Ordinal);

            var missing = "missing.config.json";
            using var stderrWithConfig = new StringWriter();
            code = await Program.RunAsync(["explore", "--config", missing], stdout, stderrWithConfig, TextReader.Null, interactive: true, CancellationToken.None, dir);

            Assert.Equal(2, code);
            Assert.Contains(
                "CONFIG_NOT_FOUND: config file not found: " + Path.GetFullPath(missing, dir) + " (" + missing + " resolved against the working directory " + dir + ")",
                stderrWithConfig.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
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
    public async Task Redacts_an_account_password_the_record_echoes_wherever_it_is_printed()
    {
        const string Password = "hunter2-secret-pw";
        using var _ = Env("E2E_USER_EXPLORE_ECHO_USERNAME", "carol@example.com");
        using var __ = Env("E2E_USER_EXPLORE_ECHO_PASSWORD", Password);
        var finding = new ReportExploreFinding(
            "f-1",
            0,
            1,
            ReportExploreFindingKind.Issue,
            3,
            "The page shows " + Password,
            "No password on the page",
            "The banner reads " + Password,
            ["Sign in", "Read " + Password],
            DateTimeOffset.UnixEpoch);
        var step = new ReportExploreStep(1, "Sign in " + Password, "Sign in", ReportExploreStepStatus.Failed, DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(1), "It printed " + Password, "STEP_FAILED");
        var fake = new FakeExplore(
            [
                new ExploreProgress.Started("Explore", Budgets),
                new ExploreProgress.StepStarted(new OpenedStep(1, step.Title, step.Instruction)),
                new ExploreProgress.FindingReported(finding),
                new ExploreProgress.StepFinished(step),
                new ExploreProgress.Finished(ReportExploreEnded.Finished, "The assessment kept " + Password),
            ],
            new ExploreOutcome(RunStatus.Failed, 1, null, Passed().Explore with { Findings = [finding], Steps = [step] }));

        var (_, stdout, stderr) = await fake.InvokeAsync();

        Assert.DoesNotContain(Password, stdout + stderr, StringComparison.Ordinal);
        Assert.Contains("The page shows <secret:", stdout, StringComparison.Ordinal);
        Assert.Contains("It printed <secret:", stdout, StringComparison.Ordinal);
        Assert.Contains("The assessment kept <secret:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bounds_a_very_long_field_before_it_reaches_the_terminal()
    {
        var title = new string('x', 100_000);
        var finding = new ReportExploreFinding("f-1", 0, null, ReportExploreFindingKind.Warning, 1, title, "e", "a", [], DateTimeOffset.UnixEpoch);
        var fake = new FakeExplore(
            [new ExploreProgress.Started("Explore", Budgets), new ExploreProgress.FindingReported(finding)],
            new ExploreOutcome(RunStatus.Passed, 0, null, Passed().Explore));

        var (_, stdout, _) = await fake.InvokeAsync();

        Assert.True(stdout.Split('\n').Max(line => line.Length) <= 8_192 + 40);
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
