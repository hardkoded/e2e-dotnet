// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.WinApp;
using Microsoft.Extensions.Time.Testing;

namespace E2E.WinApp.Tests.Helpers;

/// <summary>An engine over a <see cref="FakeWindowDriver"/>, with the settle wait counted instead of waited.</summary>
public sealed class Harness : IAsyncDisposable
{
    public static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-winapp-tests-root");

    public Harness(WinAppOptions? options = null, FakeWindowDriver? driver = null)
    {
        Driver = driver ?? new FakeWindowDriver();
        Engine = new WinAppEngine(options ?? Options(), () => Driver)
        {
            Clock = Clock,
            Delay = (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                Settles.Add(span);
                return Task.CompletedTask;
            },
        };
    }

    public FakeWindowDriver Driver { get; }

    /// <summary>The artifact directory the next session starts with; null starts it without one.</summary>
    public string? ArtifactsDir { get; set; }

    public WinAppEngine Engine { get; }

    public FakeTimeProvider Clock { get; } = new();

    /// <summary>Every settle wait the engine asked for.</summary>
    public List<TimeSpan> Settles { get; } = [];

    public List<AppLogEntry> AppLog { get; } = [];

    public List<IReadOnlyDictionary<string, string>> Environment { get; } = [];

    public WindowSession? Session { get; private set; }

    public static WinAppOptions Options(string? appPath = "bin/Desk.exe", string? bundleId = "Desk", IReadOnlyList<string>? launchArguments = null, bool settle = true) => new()
    {
        TargetName = "desk",
        App = new WinAppApp { AppPath = appPath, BundleId = bundleId, LaunchArguments = launchArguments },
        Settle = settle,
    };

    public async Task<WindowSession> StartAsync(CancellationToken cancellationToken = default)
    {
        var session = (WindowSession)await Engine.StartAsync(
            new EngineStartOptions
            {
                ProjectRoot = Root,
                ArtifactsDir = ArtifactsDir,
                AppLog = AppLog.Add,
                Environment = Environment.Add,
            },
            cancellationToken);
        Session = session;
        return session;
    }

    /// <summary>Starts a session and opens the app on the scripted settings window.</summary>
    public async Task<WindowSession> OpenAsync(RawElement? tree = null)
    {
        Driver.Trees[FakeWindowDriver.MainHandle] = tree ?? RawElements.Settings();
        var session = await StartAsync();
        await session.OpenAsync("", CancellationToken.None);
        return session;
    }

    /// <summary>The node of the latest observation with <paramref name="testId"/>, as the harness would hand it to the engine.</summary>
    public static SemanticNode ByTestId(Observation observation, string testId) =>
        Walk(observation.Roots).First(node => node.TestId == testId);

    public static SemanticNode ByName(Observation observation, string name) =>
        Walk(observation.Roots).First(node => node.Name == name);

    public static IEnumerable<SemanticNode> Walk(IEnumerable<SemanticNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Walk(node.Children))
            {
                yield return child;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Session is { } session)
        {
            await session.DisposeAsync();
        }
    }
}
