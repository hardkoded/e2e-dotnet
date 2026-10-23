// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.FailureEvidence;

/// <summary>
/// An engine over a page of buttons that masks its screenshots, and writes down what it was asked, as upstream's fake engine does
/// (<c>createFakeEngine</c>, <c>tests/helpers/fake-engine.ts</c>). <see cref="OnObserve"/> runs before every observation.
/// </summary>
internal sealed class EvidenceEngine(params string[] buttons) : IEngine
{
    private readonly string[] _buttons = buttons;
    private readonly List<string> _operations = [];

    public string Platform => "fake";

    public string Version => "0";

    public EngineCapabilities Capabilities =>
        EngineCapabilities.Observation | EngineCapabilities.Actions | EngineCapabilities.Location | (CapturesPixels ? EngineCapabilities.Screenshot : 0);

    /// <summary>Whether the engine declares screenshots; upstream's fake engine does with <c>artifacts: true</c>.</summary>
    public bool CapturesPixels { get; init; } = true;

    /// <summary>Whether every observation reports that the engine stopped listing nodes at its limit.</summary>
    public bool Truncated { get; init; }

    /// <summary>Runs before every observation; a wait, or a throw. Set it once the test body has failed to leave the failure's own look to it.</summary>
    public Func<CancellationToken, Task>? OnObserve { get; set; }

    /// <summary>What the engine was asked, in order: <c>observe</c> and <c>screenshot</c>.</summary>
    public IReadOnlyList<string> Operations
    {
        get
        {
            lock (_operations)
            {
                return [.. _operations];
            }
        }
    }

    public int Count(string operation) => Operations.Count(candidate => candidate == operation);

    public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) => Task.FromResult<IEngineSession>(new Session(this));

    private void Note(string operation)
    {
        lock (_operations)
        {
            _operations.Add(operation);
        }
    }

    private sealed class Session(EvidenceEngine engine) : IEngineSession
    {
        public string Route => "http://127.0.0.1:4599/";

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            engine.Note("observe");
            if (engine.OnObserve is { } before)
            {
                await before(cancellationToken).ConfigureAwait(false);
            }

            return new Observation
            {
                Route = Route,
                Truncated = engine.Truncated,
                Roots =
                [
                    new SemanticNode
                    {
                        Ref = "root",
                        Role = "document",
                        Children = [.. engine._buttons.Select((name, index) => new SemanticNode { Ref = "b" + index, Role = "button", Name = name })],
                    },
                ],
            };
        }

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken)
        {
            engine.Note("screenshot");
            return Task.FromResult(new EngineScreenshot(ImageOps.EncodePng(new RgbaImage(80, 40, new byte[80 * 40 * 4])), 2, MaskedRegionCount: 0));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
