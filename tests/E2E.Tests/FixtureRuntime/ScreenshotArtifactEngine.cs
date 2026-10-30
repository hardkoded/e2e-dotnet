// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.FixtureRuntime;

/// <summary>
/// An in-memory engine that keeps one screenshot artifact, the <c>artifacts: { screenshot }</c> of upstream's <c>defineEngine</c> in
/// <c>fixture-runtime.test.ts</c>, and a password field to fill. The file name is fixed, as upstream's fake engine names it.
/// </summary>
internal sealed class ScreenshotArtifactEngine(string artifact) : IEngine
{
    /// <summary>The labels each <c>app.screenshot</c> handed the engine.</summary>
    public List<string?> Labels { get; } = [];

    /// <summary>What the fill wrote into the field, or null before one.</summary>
    public string? Filled { get; private set; }

    public string Platform => "fake";

    public string Version => "1";

    public EngineCapabilities Capabilities => EngineCapabilities.None;

    public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
        Task.FromResult<IEngineSession>(new Session(this, options.ArtifactsDir, artifact));

    private sealed class Session(ScreenshotArtifactEngine owner, string? artifactsDir, string artifact) : IEngineSession
    {
        public string Route => "/";

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new Observation
            {
                Route = "/",
                Roots =
                [
                    new SemanticNode { Ref = "echo", Role = "status", Text = owner.Filled ?? "" },
                    new SemanticNode { Ref = "password", Role = "textbox", States = new NodeStates { Secure = true } },
                ],
            });

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
        {
            if (action is LocatorAction.Fill fill)
            {
                owner.Filled = fill.Value;
            }

            return Task.CompletedTask;
        }

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string> ScreenshotArtifactAsync(string? label, CancellationToken cancellationToken)
        {
            owner.Labels.Add(label);
            var absolute = Path.Combine(artifactsDir!, artifact);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllBytes(absolute, [0x89, 0x50, 0x4E, 0x47]);
            return Task.FromResult(artifact);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
