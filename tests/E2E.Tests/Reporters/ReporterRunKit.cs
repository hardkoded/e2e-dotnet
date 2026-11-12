// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using E2E.Reports;

namespace E2E.Tests.Reporters;

/// <summary>
/// Test helper: one run as the run fixtures drive it, over a fake engine. Upstream's reporter tests run a project; the port runs sessions, which are its
/// tests, between <see cref="HostRun.Begin(IReadOnlyList{Reporter})"/> and <see cref="HostRun.FinishAsync"/>.
/// </summary>
internal sealed class ReporterRunKit : IDisposable
{
    private readonly string _output = Directory.CreateTempSubdirectory("e2e-reporter-run-").FullName;

    public ReporterRunKit()
    {
        AbandonedWork.Reset();
    }

    public string ProjectRoot => _output;

    public string Results => Path.Combine(_output, "results");

    public void Dispose()
    {
        Directory.Delete(_output, recursive: true);
    }

    /// <summary>One passing test that keeps a screenshot, as a session of the fake engine.</summary>
    public async Task<E2ESession> StartAsync(string title = "shows the submit button", int attempt = 1, RecordingMode trace = RecordingMode.On, string? file = "E2E.Tests.Reporters.ReporterRun", bool emitRunEvents = true) =>
        await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new ArtifactEngine(),
            TestTitle = title,
            TestFile = file,
            BaseUrl = "http://127.0.0.1:4100",
            ProjectRoot = _output,
            ResultsDirectory = Results,
            CacheEnabled = false,
            Trace = new ResolvedRecording(trace, RecordingSource.Target),
            Attempt = attempt,
            EmitRunEvents = emitRunEvents,
        });

    /// <summary>Runs the test that passes: opens the app, keeps a screenshot, and completes.</summary>
    public async Task PassAsync(string title = "shows the submit button")
    {
        await using var session = await StartAsync(title);
        await session.App.OpenAsync("/");
        await session.App.ScreenshotAsync("submit");
        session.Complete();
    }

    /// <summary>
    /// A whole run: its start and plan, the passing test, and its end, with <paramref name="reporters"/> listening. Returns what the process's report of this run
    /// said, with stderr of the run beside it.
    /// </summary>
    public async Task<(FinishedRun Finished, string Stderr)> RunAsync(IReadOnlyList<Reporter> reporters, TimeSpan? timeout = null, CancellationToken force = default)
    {
        var stderr = new StringWriter();
        var original = Console.Error;
        Console.SetError(stderr);
        try
        {
            using var run = HostRun.Begin(reporters);
            HostRun.Started(E2EConfig.Default(_output));
            HostRun.Planned("web", [new DiscoveredTest("case", "E2E.Tests.Reporters", "ReporterRun", "shows the submit button", [])], new Dictionary<string, string>());
            await PassAsync();
            await run.FinishAsync(timeout, force);
        }
        finally
        {
            Console.SetError(original);
        }

        return (await FinishedAsync(), stderr.ToString());
    }

    /// <summary>The run of this kit, as the process's report file read back.</summary>
    public async Task<FinishedRun> FinishedAsync() => (await ReportFile.FinishedRunsAsync()).Single(run => run.ArtifactsRoot == Results);

    private sealed class ArtifactEngine : IEngine
    {
        public string Platform => "document";

        public string Version => "test";

        public EngineCapabilities Capabilities => EngineCapabilities.Observation | EngineCapabilities.Actions | EngineCapabilities.Location;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) => Task.FromResult<IEngineSession>(new Session(options.ArtifactsDir!));
    }

    private sealed class Session(string artifactsDir) : IEngineSession
    {
        public string Route => "/";

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => Task.FromResult(new Observation
        {
            Route = "/",
            Roots = [new SemanticNode { Ref = "root", Role = "document", Children = [new SemanticNode { Ref = "submit", Role = "button", Name = "Submit" }] }],
        });

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<string> ScreenshotArtifactAsync(string? label, CancellationToken cancellationToken)
        {
            var relative = "screenshots/001-" + label + ".png";
            Directory.CreateDirectory(Path.Combine(artifactsDir, "screenshots"));
            await File.WriteAllBytesAsync(Path.Combine(artifactsDir, relative), [0x89, 0x50, 0x4E, 0x47], cancellationToken);
            return relative;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
