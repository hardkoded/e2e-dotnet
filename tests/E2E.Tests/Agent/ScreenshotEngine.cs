// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.Agent;

/// <summary>
/// A document engine that declares screenshots. <paramref name="shot"/> makes the picture of the nth capture; without it
/// the engine declares the capture and cannot produce it, as an engine that captures no pixels does. The picture
/// covers as many regions as the newest observation listed secure nodes, as a web page whose secure fields are all masked does.
/// </summary>
internal sealed class ScreenshotEngine(DocumentWorld world, Func<int, byte[]>? shot = null) : IEngine
{
    private readonly DocumentEngine _inner = new(world);
    private readonly Func<int, byte[]>? _shot = shot;
    private int _screenshots;

    public int Screenshots => Volatile.Read(ref _screenshots);

    public string Platform => _inner.Platform;

    public string Version => _inner.Version;

    public EngineCapabilities Capabilities => _inner.Capabilities | EngineCapabilities.Screenshot;

    /// <summary>A white PNG of one shade, so two captures with different shades differ in their bytes.</summary>
    public static byte[] Png(byte shade = 255, int width = 4, int height = 2)
    {
        var data = new byte[width * height * 4];
        for (var at = 0; at < data.Length; at += 4)
        {
            data[at] = data[at + 1] = data[at + 2] = shade;
            data[at + 3] = 255;
        }

        return ImageOps.EncodePng(new RgbaImage(width, height, data));
    }

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        return new Session(this, await _inner.StartAsync(options, cancellationToken).ConfigureAwait(false));
    }

    private sealed class Session(ScreenshotEngine engine, IEngineSession inner) : IEngineSession
    {
        private int _secure;

        public string Route => inner.Route;

        public Task OpenAsync(string url, CancellationToken cancellationToken) => inner.OpenAsync(url, cancellationToken);

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            var observation = await inner.ObserveAsync(cancellationToken).ConfigureAwait(false);
            _secure = LocatorResolver.Walk(observation.Roots).Count(node => node.States.Secure);
            return observation;
        }

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => inner.PerformAsync(node, action, cancellationToken);

        public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

        public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken)
        {
            if (engine._shot is null)
            {
                return inner.ScreenshotAsync(cancellationToken);
            }

            return Task.FromResult(new EngineScreenshot(engine._shot(Interlocked.Increment(ref engine._screenshots)), 1, _secure));
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
