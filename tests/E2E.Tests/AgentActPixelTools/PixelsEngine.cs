// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentActPixelTools;

/// <summary>
/// An engine over another that declares <see cref="EngineCapabilities.Screenshot"/> and answers with a generated 1280 by 720 PNG that changes
/// after every action, so a fresh capture differs from the one before. With <see cref="TreeUnavailable"/> its looks that ask for the pixel
/// fallback answer with no tree and the screen's pixels, as upstream's <c>kind: 'pixels'</c> observation does.
/// </summary>
internal sealed class PixelsEngine(IEngine inner) : IEngine
{
    private int _shots;

    /// <summary>Whether a look that asks for the pixel fallback finds the semantic tree unreadable.</summary>
    public bool TreeUnavailable { get; set; }

    /// <summary>Whether the unreadable look answers with no tree even when it did not ask for the pixel fallback, as upstream's fake engines do.</summary>
    public bool IgnoresPixelFallback { get; set; }

    /// <summary>Whether the unreadable look answers without pixels, as an engine that cannot prove the screen masked does.</summary>
    public bool WithholdPixels { get; set; }

    /// <summary>The number of screenshots the engine takes before it captures no more, as an engine that loses its pixels does. Unlimited by default.</summary>
    public int ShotsBeforeLoss { get; set; } = int.MaxValue;

    /// <summary>The options of every look the engine answered, in order: what the runtime asked each one for.</summary>
    public List<ObserveOptions?> Looks { get; } = [];

    /// <summary>The number of screenshots the engine took.</summary>
    public int Shots => Volatile.Read(ref _shots);

    public string Platform => inner.Platform;

    public string Version => inner.Version;

    public EngineCapabilities Capabilities => inner.Capabilities | EngineCapabilities.Screenshot;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
        new Session(this, await inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

    private EngineScreenshot Capture()
    {
        if (Shots >= ShotsBeforeLoss)
        {
            throw new EngineException(EngineErrorCodes.UnsupportedCapability, "this engine captures no more pixels", retryable: false);
        }

        var shade = (byte)(40 * Interlocked.Increment(ref _shots) % 250);
        var data = new byte[1280 * 720 * 4];
        for (var at = 0; at < data.Length; at += 4)
        {
            data[at] = shade;
            data[at + 1] = 90;
            data[at + 2] = 200;
            data[at + 3] = 255;
        }

        return new EngineScreenshot(ImageOps.EncodePng(new RgbaImage(1280, 720, data)), 1, 0);
    }

    private sealed class Session(PixelsEngine engine, IEngineSession session) : IEngineSession
    {
        public string Route => session.Route;

        public Task OpenAsync(string url, CancellationToken cancellationToken) => session.OpenAsync(url, cancellationToken);

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => session.ObserveAsync(cancellationToken);

        public async Task<Observation> ObserveAsync(ObserveOptions? options, TimeSpan timeout, CancellationToken cancellationToken)
        {
            lock (engine.Looks)
            {
                engine.Looks.Add(options);
            }

            if (engine.TreeUnavailable && (options?.PixelFallback == true || engine.IgnoresPixelFallback))
            {
                return new Observation { Route = session.Route, Roots = [], TreeUnavailable = true, Pixels = engine.WithholdPixels ? null : engine.Capture() };
            }

            return await session.ObserveAsync(options, timeout, cancellationToken).ConfigureAwait(false);
        }

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => session.PerformAsync(node, action, cancellationToken);

        public Task PressAsync(string key, CancellationToken cancellationToken) => session.PressAsync(key, cancellationToken);

        public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => session.SwipeAsync(direction, cancellationToken);

        public Task BackAsync(CancellationToken cancellationToken) => session.BackAsync(cancellationToken);

        public Task RestartAsync(CancellationToken cancellationToken) => session.RestartAsync(cancellationToken);

        public Task ClearStateAsync(CancellationToken cancellationToken) => session.ClearStateAsync(cancellationToken);

        public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) => Task.FromResult(engine.Capture());

        public ValueTask DisposeAsync() => session.DisposeAsync();
    }
}
