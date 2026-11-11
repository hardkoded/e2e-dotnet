// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using E2E.Cli.Mcp;

namespace E2E.NUnit.Tests.McpTestRun;

/// <summary>
/// <c>describe('sessions side by side')</c> of <c>tests/integration/mcp-test-run.test.ts</c> (tester-army/e2e#1315). Both <c>it</c>s run through
/// <see cref="SessionHost"/> in process, as upstream's <c>inProcessHost</c> does, on a real browser at the testbed. Both are <c>Partial_</c>: the
/// open's text is the port's (<c>Test "&lt;full name&gt;" ended without failing after ...</c>, which names the test by its NUnit full name and does not
/// say <c>passed</c> or give <c>file:line</c>, see COMPATIBILITY.md), so the tests check the outcome and not the text upstream matches. The tests run in
/// a load context of their own (the test assembly loads again on every open), so the fixtures reach the test driver through <see cref="AppContext"/>
/// data, as upstream's fixtures reach it through <c>globalThis</c>. Upstream's top-level <c>await</c> that holds collection's slot has no .NET
/// equivalent (collection runs once per load, not per turn): <see cref="SlowLoadFixture"/> holds its set-up for the same 300 ms instead. Collection
/// (<c>SessionTests.SelectOne</c>) takes no turn: it reads the test assembly and runs no user code.
/// </summary>
public sealed class SessionsSideBySideTests
{
    /// <summary>The <see cref="AppContext"/> keys the fixtures read; the fixtures are in this file, so they share these names.</summary>
    internal const string HeldKey = "e2e.side-by-side.held";
    internal const string GateKey = "e2e.side-by-side.gate";
    internal const string MarkKey = "e2e.side-by-side.mark";
    internal const string SpansKey = "e2e.side-by-side.spans";

    /// <summary>How long a fixture waits for the test driver to release it, so a driver that failed early does not hold the turn forever.</summary>
    internal static readonly TimeSpan GateWait = TimeSpan.FromMinutes(2);

    private static readonly string HeldName = typeof(HeldTurnFixture).FullName + "." + nameof(HeldTurnFixture.Holds_its_turn);
    private static readonly string MarkedName = typeof(MarkedTurnFixture).FullName + "." + nameof(MarkedTurnFixture.Marks);
    private static readonly string SlowName = typeof(SlowLoadFixture).FullName + "." + nameof(SlowLoadFixture.Loads_slowly);
    private static readonly string QuickName = typeof(QuickLoadFixture).FullName + "." + nameof(QuickLoadFixture.Loads_quickly);

    [Test]
    [NonParallelizable]
    public async Task Partial_Runs_none_of_a_test_whose_open_was_cancelled_while_it_waited_for_its_turn()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-side-by-side-").FullName;
        var marked = Path.Combine(dir, "marked.txt");
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        AppContext.SetData(HeldKey, held);
        AppContext.SetData(GateKey, gate);
        AppContext.SetData(MarkKey, marked);
        var sessions = Host(dir);
        try
        {
            var holding = sessions.OpenAsync(new E2E.Cli.Mcp.OpenSessionOptions { Test = HeldName });
            await held.Task.WaitAsync(TimeSpan.FromSeconds(60));

            using var cancel = new CancellationTokenSource();
            var waiting = sessions.OpenAsync(new E2E.Cli.Mcp.OpenSessionOptions { Test = MarkedName }, cancel.Token);
            // Long enough for the second session to boot and queue behind the first.
            await Task.Delay(TimeSpan.FromSeconds(3));
            await cancel.CancelAsync();
            var cancelled = await Assert.ThrowsAsync<E2EException>(async () => await waiting);
            Assert.That(cancelled!.Code, Is.EqualTo("CANCELLED"));

            gate.TrySetResult();
            Assert.That(await holding, Does.StartWith("Test \"" + HeldName + "\" ended without failing"));
            await sessions.CloseAllAsync("done");
            Assert.That(File.Exists(marked), Is.False);
        }
        finally
        {
            // Released even when an assertion failed above, so the held fixture does not keep the turn.
            gate.TrySetResult();
            await sessions.CloseAllAsync("done");
            sessions.Dispose();
            AppContext.SetData(HeldKey, null);
            AppContext.SetData(GateKey, null);
            AppContext.SetData(MarkKey, null);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    [NonParallelizable]
    public async Task Partial_Collect_and_run_their_tests_one_at_a_time_and_both_pass()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-side-by-side-").FullName;
        var spans = new ConcurrentQueue<(DateTime Start, DateTime End)>();
        AppContext.SetData(SpansKey, spans);
        var sessions = Host(dir);
        try
        {
            var opened = await Task.WhenAll(sessions.OpenAsync(new E2E.Cli.Mcp.OpenSessionOptions { Test = SlowName }), sessions.OpenAsync(new E2E.Cli.Mcp.OpenSessionOptions { Test = QuickName }));
            Assert.That(opened[0], Does.StartWith("Test \"" + SlowName + "\" ended without failing"));
            Assert.That(opened[1], Does.StartWith("Test \"" + QuickName + "\" ended without failing"));
            await sessions.CloseAllAsync("done");

            // Each body records when it ran; one at a time means no two of them overlap.
            var ordered = spans.OrderBy(span => span.Start).ToList();
            Assert.That(ordered, Has.Count.EqualTo(2));
            Assert.That(ordered[0].End, Is.LessThanOrEqualTo(ordered[1].Start));
        }
        finally
        {
            await sessions.CloseAllAsync("done");
            sessions.Dispose();
            AppContext.SetData(SpansKey, null);
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Records one body's span for the overlap check.</summary>
    internal static void RecordSpan(DateTime start)
    {
        if (AppContext.GetData(SpansKey) is ConcurrentQueue<(DateTime Start, DateTime End)> spans)
        {
            spans.Enqueue((start, DateTime.UtcNow));
        }
    }

    /// <summary>A host on this assembly and the testbed, as upstream's <c>inProcessHost</c> is on its project.</summary>
    internal static SessionHost Host(string dir)
    {
        var config = """{ "testAssembly": """ + System.Text.Json.JsonSerializer.Serialize(typeof(SessionsSideBySideTests).Assembly.Location)
            + """, "targets": [{ "name": "web", "platform": "web", "app": { "url": """ + System.Text.Json.JsonSerializer.Serialize(E2E.Playground.Testbed.Url) + """ } }] }""";
        return new SessionHost(new SessionHostOptions
        {
            LocateConfig = _ => Path.Combine(dir, "e2e.config.json"),
            LoadConfig = _ => Task.FromResult(E2EConfig.Parse(config, dir, _ => null)),
            Log = (_, _) => { },
        });
    }
}

/// <summary>The test <see cref="SessionsSideBySideTests"/> holds its turn with.</summary>
[Explicit("Run by SessionsSideBySideTests")]
public sealed class HeldTurnFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", SourceDirectory(), _ => null);

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Holds_its_turn()
    {
        await App.OpenAsync("/");
        ((TaskCompletionSource)AppContext.GetData(SessionsSideBySideTests.HeldKey)!).TrySetResult();
        await ((TaskCompletionSource)AppContext.GetData(SessionsSideBySideTests.GateKey)!).Task.WaitAsync(SessionsSideBySideTests.GateWait);
    }
}

/// <summary>The test <see cref="SessionsSideBySideTests"/> must never run; its set-up writes a mark only a run of it leaves.</summary>
[Explicit("Run by SessionsSideBySideTests")]
public sealed class MarkedTurnFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", SourceDirectory(), _ => null);

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [OneTimeSetUp]
    public void MarkTheRun() => File.WriteAllText((string)AppContext.GetData(SessionsSideBySideTests.MarkKey)!, "ran");

    [Test]
    public async Task Marks()
    {
        await App.OpenAsync("/");
    }
}

/// <summary>The slow of the two tests <see cref="SessionsSideBySideTests"/> opens together.</summary>
[Explicit("Run by SessionsSideBySideTests")]
public sealed class SlowLoadFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", SourceDirectory(), _ => null);

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [OneTimeSetUp]
    public void LoadSlowly() => Thread.Sleep(300);

    [Test]
    public async Task Loads_slowly()
    {
        var start = DateTime.UtcNow;
        await App.OpenAsync("/");
        // Each poll takes a while, so two bodies overlap unless they take turns.
        await Expect.Poll(async () =>
        {
            await Task.Delay(300);
            return 1;
        }).ToMatchAsync(Is.EqualTo(1));
        SessionsSideBySideTests.RecordSpan(start);
    }
}

/// <summary>The quick of the two tests <see cref="SessionsSideBySideTests"/> opens together.</summary>
[Explicit("Run by SessionsSideBySideTests")]
public sealed class QuickLoadFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse("""{ "cache": { "mode": "off" } }""", SourceDirectory(), _ => null);

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Loads_quickly()
    {
        var start = DateTime.UtcNow;
        await App.OpenAsync("/");
        await Expect.Poll(async () =>
        {
            await Task.Delay(300);
            return 1;
        }).ToMatchAsync(Is.EqualTo(1));
        SessionsSideBySideTests.RecordSpan(start);
    }
}
