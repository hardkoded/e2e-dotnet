// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using ManagedProcessUnderTest = E2E.ManagedProcess;

namespace E2E.Tests.ManagedProcess;

/// <summary>
/// The stall diagnostics of upstream's <c>describe('ManagedProcess stall diagnostics')</c>. The port's tests wait out a real budget where
/// upstream moves a fake clock past it, so the budgets are two seconds, and the two notice tests wait for the half-way mark of ten.
/// </summary>
public sealed class ManagedProcessStallDiagnosticsTests : IDisposable
{
    private static readonly string Log = Path.Combine("out", "services.log");
    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-stall-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>A budget of two seconds: the test lets the child print, then waits out the budget.</summary>
    private static CommandConfig Stalled(string[] args, string? log = null, TimeSpan? startupTimeout = null, IReadOnlyDictionary<string, string>? env = null, IReadOnlyList<string>? inheritEnv = null) =>
        AppProgram.Command(args, startupTimeout ?? TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), log, env: env, inheritEnv: inheritEnv);

    /// <summary>A server that answers every probe with a 503, where upstream's tests probe a port nothing listens on: a refused connection to a closed port takes seconds on Windows.</summary>
    private static TestServer NotReady() => new(_ => Task.FromResult<int?>(503));

    private async Task<EngineException> FailureOf(ManagedProcessUnderTest app, string? printed = null)
    {
        var pending = Assert.ThrowsAsync<EngineException>(() => app.StartAsync());
        if (printed is not null)
        {
            await AppProgram.Logged(Path.Combine(_dir, Log), printed);
        }

        var failure = await pending;
        Assert.Equal("APP_UNREACHABLE", failure.Code);
        return failure;
    }

    [Fact]
    public async Task Ends_a_readiness_timeout_with_the_lines_the_process_appended_controls_stripped_earlier_runs_left_out()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "out"));
        await File.WriteAllTextAsync(Path.Combine(_dir, Log), "from an earlier run\n");
        await using var notReady = NotReady();
        var app = new ManagedProcessUnderTest(
            "target \"web\" command",
            Stalled(["print", "stay", "booting", "\u001b[32mlistening soon\u001b[0m", ""], Log),
            _dir,
            new Readiness(notReady.Url));
        var failure = await FailureOf(app, "listening soon");
        Assert.Equal(
            $"target \"web\" command was not reachable at {notReady.Url} within 2000 ms\noutput in {Log} since target \"web\" command started:\n  booting\n  listening soon",
            failure.Message);
        await app.StopAsync();
    }

    [Fact]
    public async Task Ends_an_early_exit_with_the_same_tail()
    {
        await using var notReady = NotReady();
        var app = new ManagedProcessUnderTest(
            "target \"web\" command",
            Stalled(["print", "1", "migrations pending", "fatal: database \"app\" does not exist"], Log),
            _dir,
            new Readiness(notReady.Url));
        var failure = await Assert.ThrowsAsync<EngineException>(() => app.StartAsync());
        Assert.Equal("APP_UNREACHABLE", failure.Code);
        Assert.Equal(
            $"target \"web\" command exited with code 1 before becoming ready\noutput in {Log} since target \"web\" command started:\n  migrations pending\n  fatal: database \"app\" does not exist",
            failure.Message);
    }

    [Fact]
    public async Task Ends_a_waitForExit_timeout_with_the_tail_and_a_budget_under_10_s_never_narrates_progress()
    {
        var notices = new List<string>();
        var app = new ManagedProcessUnderTest(
            "service \"compose\"",
            Stalled(["print", "stay", " Image postgres:16.4-alpine Pulling ", " Image redis:7-alpine Pulling "], Log),
            _dir,
            Readiness.WaitForExit,
            new ManagedProcessHooks { Notice = notices.Add });
        var failure = await FailureOf(app, "redis");
        Assert.Equal(
            $"service \"compose\" did not exit within 2000 ms\noutput in {Log} since service \"compose\" started:\n   Image postgres:16.4-alpine Pulling\n   Image redis:7-alpine Pulling",
            failure.Message);
        Assert.Empty(notices);
    }

    /// <summary>The app prints each line as its own line, so the quote's lines are the lines printed.</summary>
    private static string[] Printed(IEnumerable<string> lines) => ["print", "stay", .. lines];

    [Fact]
    public async Task Quotes_every_line_the_command_appended_when_the_log_since_it_started_fits_both_windows()
    {
        var lines = Enumerable.Range(1, 25).Select(i => "line " + i).ToArray();
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(Printed(lines), Log), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app, "line 25");
        var quoted = failure.Message.Split('\n').Skip(2).ToArray();
        Assert.Equal(lines.Select(line => "  " + line), quoted);
    }

    [Fact]
    public async Task Quotes_the_first_and_last_20_lines_and_only_those_of_a_log_longer_than_both_windows()
    {
        var lines = Enumerable.Range(1, 300).Select(i => $"line {i:D3} {new string('x', 40)}").ToArray();
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(Printed(lines), Log), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app, lines[^1]);
        var quoted = failure.Message.Split('\n').Skip(2).ToArray();
        Assert.Equal(41, quoted.Length);
        Assert.Equal(lines.Take(20).Select(line => "  " + line), quoted.Take(20));
        Assert.Equal("  [startup output omitted]", quoted[20]);
        Assert.Equal(lines.TakeLast(20).Select(line => "  " + line), quoted.Skip(21));
    }

    [Fact]
    public async Task Quotes_whole_lines_only_when_the_windows_cut_inside_a_line_at_either_edge()
    {
        // Long lines come back as one long command line, so the child builds them.
        var lines = Enumerable.Range(1, 60).Select(i => $"line {i:D2} {new string('y', 280)}").ToArray();
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(Printed(lines), Log), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app, lines[^1]);
        var quoted = failure.Message.Split('\n').Skip(2).ToArray();
        var printed = quoted.Where(line => line != "  [startup output omitted]").Select(line => line[2..]).ToArray();
        foreach (var line in printed)
        {
            Assert.Contains(line, lines);
        }

        Assert.Contains("  [startup output omitted]", quoted);
        Assert.Equal(lines[0], printed[0]);
        Assert.Equal(lines[^1], printed[^1]);
    }

    [Fact]
    public async Task Keeps_the_complete_line_before_a_whitespace_fragment_the_head_window_cut_into()
    {
        // The head window ends a few bytes into this line's indent, so the fragment it cut is whitespace alone: filtering empty lines first
        // would drop it, and the pop that removes the fragment would then take the fatal line above it.
        var fatal = "fatal: " + new string('x', 4_080);
        var lines = new[] { fatal, "            at Object.<anonymous> (/app/server.js:12:3)" }
            .Concat(Enumerable.Range(1, 200).Select(i => $"watcher: rebuilt module {i}"))
            .ToArray();
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(Printed(lines), Log), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app, lines[^1]);
        var quoted = failure.Message.Split('\n').Skip(2).ToArray();
        Assert.Equal("  " + fatal, quoted[0]);
    }

    [Fact]
    public async Task Leaves_an_earlier_run_out_when_the_tail_window_reaches_back_into_it()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "out"));
        var earlier = Enumerable.Range(1, 200).Select(i => $"earlier run: line {i}");
        await File.WriteAllTextAsync(Path.Combine(_dir, Log), string.Join('\n', earlier) + "\n");
        var lines = Enumerable.Range(1, 40).Select(i => $"added {i}").ToArray();
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(Printed(lines), Log), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app, "added 40");
        Assert.Equal(lines.Select(line => "  " + line), failure.Message.Split('\n').Skip(2));
    }

    [Fact]
    public async Task Quotes_the_first_and_last_20_lines_of_a_log_that_fits_the_byte_window_but_not_the_line_budget()
    {
        var lines = Enumerable.Range(1, 50).Select(i => $"line {i}").ToArray();
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(Printed(lines), Log), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app, "line 50");
        var quoted = failure.Message.Split('\n').Skip(2).ToArray();
        Assert.Equal(41, quoted.Length);
        Assert.Equal(lines.Take(20).Select(line => "  " + line), quoted.Take(20));
        Assert.Equal("  [startup output omitted]", quoted[20]);
        Assert.Equal(lines.TakeLast(20).Select(line => "  " + line), quoted.Skip(21));
    }

    [Fact]
    public async Task Keeps_whole_lines_at_both_window_edges_when_the_cut_lands_exactly_between_two_lines()
    {
        // Each line with its newline is 256 bytes, so the 4 KiB window edges fall on line boundaries at both ends, whatever the newline is.
        var pad = new string('z', 256 - "line 01 ".Length - Environment.NewLine.Length);
        var lines = Enumerable.Range(1, 60).Select(i => $"line {i:D2} {pad}").ToArray();
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(Printed(lines), Log), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app, lines[^1]);
        var quoted = failure.Message.Split('\n').Skip(2).ToArray();
        Assert.Equal(33, quoted.Length);
        Assert.Equal(lines.Take(16).Select(line => "  " + line), quoted.Take(16));
        Assert.Equal("  [startup output omitted]", quoted[16]);
        Assert.Equal(lines.TakeLast(16).Select(line => "  " + line), quoted.Skip(17));
    }

    [Fact]
    public async Task Keeps_the_marker_when_the_head_window_sits_inside_one_huge_first_line()
    {
        var lines = new[] { "fatal: " + new string('x', 5_000) }
            .Concat(Enumerable.Range(1, 400).Select(i => $"watcher: rebuilt module {i}"))
            .ToArray();
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(Printed(lines), Log), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app, "watcher: rebuilt module 400");
        var quoted = failure.Message.Split('\n').Skip(2).ToArray();
        Assert.Equal(21, quoted.Length);
        Assert.Equal("  [startup output omitted]", quoted[0]);
        Assert.Equal("  watcher: rebuilt module 400", quoted[20]);
    }

    [Fact]
    public async Task Quotes_a_log_of_25_long_lines_whole_while_it_stays_inside_the_byte_window()
    {
        var lines = Enumerable.Range(1, 25).Select(i => $"line {i:D2} {new string('w', 241)}").ToArray();
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(Printed(lines), Log), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app, lines[^1]);
        Assert.Equal(lines.Select(line => "  " + line), failure.Message.Split('\n').Skip(2));
    }

    [Fact]
    public async Task Quotes_whole_lines_around_the_marker_when_a_short_log_has_lines_longer_than_the_byte_window()
    {
        var lines = Enumerable.Range(1, 10).Select(i =>
        {
            var prefix = $"line {i} ";
            return prefix + new string('v', 1_024 - prefix.Length);
        }).ToArray();
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(Printed(lines), Log), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app, lines[^1][..24]);
        var quoted = failure.Message.Split('\n').Skip(2).ToArray();
        Assert.Equal(7, quoted.Length);
        Assert.Equal(lines.Take(3).Select(line => "  " + line), quoted.Take(3));
        Assert.Equal("  [startup output omitted]", quoted[3]);
        Assert.Equal(lines.TakeLast(3).Select(line => "  " + line), quoted.Skip(4));
    }

    [Fact]
    public async Task Keeps_the_first_line_the_command_wrote_when_the_log_it_appends_to_did_not_end_with_one()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "out"));
        await File.WriteAllTextAsync(Path.Combine(_dir, Log), "from an earlier run that never finished its line");
        var lines = new[] { "fatal: EADDRINUSE" }.Concat(Enumerable.Range(1, 400).Select(i => $"watcher: rebuilt module {i}")).ToArray();
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(Printed(lines), Log), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app, "watcher: rebuilt module 400");
        Assert.Equal("  fatal: EADDRINUSE", failure.Message.Split('\n').Skip(2).First());
    }

    [Fact]
    public async Task Redacts_command_env_values_from_the_quoted_output_with_the_report_wide_secret_markers_the_log_itself_untouched()
    {
        var app = new ManagedProcessUnderTest(
            "service \"compose\"",
            Stalled(["print-template", "token=${SECRET} port=${PORT}"], Log, env: new Dictionary<string, string> { ["SECRET"] = "hunter2-hunter2", ["PORT"] = "443" }),
            _dir,
            Readiness.WaitForExit);
        var failure = await FailureOf(app, "token=");
        Assert.Equal(
            $"service \"compose\" did not exit within 2000 ms\noutput in {Log} since service \"compose\" started:\n  token=<secret:SECRET> port=443",
            failure.Message);
        Assert.Contains("token=hunter2-hunter2", await File.ReadAllTextAsync(Path.Combine(_dir, Log)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redacts_inheritEnv_values_from_the_quoted_output_too()
    {
        Environment.SetEnvironmentVariable("E2E_TEST_INHERITED_TOKEN", "inherited-token-value");
        try
        {
            var app = new ManagedProcessUnderTest(
                "service \"compose\"",
                Stalled(["print-template", "token=${E2E_TEST_INHERITED_TOKEN}"], Log, inheritEnv: ["E2E_TEST_INHERITED_*"]),
                _dir,
                Readiness.WaitForExit);
            var failure = await FailureOf(app, "token=");
            Assert.Contains("  token=<secret:E2E_TEST_INHERITED_TOKEN>", failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("E2E_TEST_INHERITED_TOKEN", null);
        }
    }

    [Fact]
    public async Task Points_at_log_when_the_command_has_none()
    {
        var app = new ManagedProcessUnderTest("service \"compose\"", Stalled(["print", "stay", "lost without a log"]), _dir, Readiness.WaitForExit);
        var failure = await FailureOf(app);
        Assert.Equal("service \"compose\" did not exit within 2000 ms\nset log on this command to keep its output", failure.Message);
        Assert.False(Directory.Exists(Path.Combine(_dir, "out")));
    }

    [Fact]
    public async Task Narrates_once_at_half_the_budget_while_the_wait_goes_on()
    {
        // The wait is clocked in real time, so the test reads when the notice came instead of sampling at fixed moments.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var notices = new List<(string Message, TimeSpan At)>();
        var app = new ManagedProcessUnderTest(
            "service \"compose\"",
            Stalled(["idle"], Log, TimeSpan.FromSeconds(10)),
            _dir,
            Readiness.WaitForExit,
            new ManagedProcessHooks { Notice = message => { lock (notices) { notices.Add((message, clock.Elapsed)); } } });
        var failure = await Assert.ThrowsAsync<EngineException>(() => app.StartAsync());
        Assert.Equal("APP_UNREACHABLE", failure.Code);
        Assert.Contains("did not exit within 10000 ms", failure.Message, StringComparison.Ordinal);
        lock (notices)
        {
            var notice = Assert.Single(notices);
            Assert.Equal($"service \"compose\" still starting after 5s: waiting for it to exit; log: {Log}", notice.Message);

            // At the half mark itself, not at the next 250 ms tick, and not before it.
            Assert.InRange(notice.At, TimeSpan.FromSeconds(4.95), TimeSpan.FromSeconds(7));
        }
    }

    [Fact]
    public async Task Names_the_URL_it_waits_for_on_a_readyUrl_command()
    {
        var notices = new List<string>();
        await using var notReady = NotReady();
        var app = new ManagedProcessUnderTest(
            "target \"web\" command",
            Stalled(["idle"], startupTimeout: TimeSpan.FromSeconds(10)),
            _dir,
            new Readiness(notReady.Url),
            new ManagedProcessHooks { Notice = message => { lock (notices) { notices.Add(message); } } });
        var failure = await Assert.ThrowsAsync<EngineException>(() => app.StartAsync());
        Assert.Equal("APP_UNREACHABLE", failure.Code);
        lock (notices)
        {
            Assert.Equal([$"target \"web\" command still starting after 5s: waiting for {notReady.Url}"], notices);
        }
    }
}
