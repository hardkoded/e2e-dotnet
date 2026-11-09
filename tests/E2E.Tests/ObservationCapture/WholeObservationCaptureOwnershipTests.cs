// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.ObservationCapture;

/// <summary>
/// Upstream's <c>describe('whole observation capture ownership')</c> (<c>packages/web/tests/unit/observation-capture.test.ts</c>, tester-army/e2e
/// at <c>f185ca8</c>, #1582). Upstream scripts a browser whose page returns handles and holds one stage of the read. The port reads a real page:
/// <see cref="WebEngine.ReadFrame"/> hands the reader a <c>HeldHandle</c> around the real handle the page script returned, which counts
/// disposals and holds the stage's reply; <see cref="WebEngine.CapturePage"/> counts and holds the screenshot.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class WholeObservationCaptureOwnershipTests
{
    private static readonly TimeSpan Operation = TimeSpan.FromSeconds(2);

    // Upstream's it.each also covers 'element' and 'frame': the port makes element handles on demand (ElementList.FetchAsync), not during the
    // capture, so neither stage exists to stall. Both are skipped, and named in the PR report.
    [Theory]
    [InlineData("evaluation")]
    [InlineData("metadata")]
    [InlineData("elements")]
    public async Task Partial_Recovers_a_stalled_stage_and_disposes_late_handles(string stage)
    {
        await using var fixture = await Fixture.StartAsync(stage);

        var result = await fixture.Session.ObserveAsync(new ObserveOptions { PixelFallback = true }, Operation, CancellationToken.None);

        Assert.True(result.TreeUnavailable);
        Assert.Equal(1, fixture.Screenshots);
        fixture.Gate.SetResult();
        await Fixture.UntilAsync(() => fixture.Captured.Count == 1 && fixture.Captured[0].Disposals == 1);
        var captured = fixture.Captured[0];
        Assert.Equal(1, captured.Disposals);
        if (stage == "evaluation")
        {
            Assert.Equal(0, captured.Evaluations);
        }

        if (stage == "metadata")
        {
            Assert.Equal(0, captured.Properties);
        }

        if (stage == "elements")
        {
            await Fixture.UntilAsync(() => fixture.Elements.Count == 1 && fixture.Elements[0].Disposals == 1);
            Assert.Equal(1, fixture.Elements[0].Disposals);
        }
    }

    [Fact]
    public async Task Times_out_stalled_metadata_within_the_operation_budget_when_fallback_is_disabled()
    {
        await using var fixture = await Fixture.StartAsync("metadata");

        var failure = await Assert.ThrowsAsync<EngineException>(() => fixture.Session.ObserveAsync(null, Operation, CancellationToken.None));

        Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
        Assert.Equal(0, fixture.Screenshots);
        Assert.Equal(1, fixture.Captured[0].Disposals);
        fixture.Gate.SetResult();
        await Task.Delay(100);
        Assert.Equal(0, fixture.Captured[0].Properties);
    }

    // Partial: upstream also asserts the first node's element handle was never made; the port makes element handles on demand, so none is made
    // during the capture whatever the disposal does.
    [Fact]
    public async Task Partial_Does_not_spend_the_capture_budget_waiting_for_metadata_disposal()
    {
        await using var fixture = await Fixture.StartAsync(null);
        fixture.NeverDispose = true;
        var started = Stopwatch.StartNew();

        var result = await fixture.Session.ObserveAsync(null, Operation, CancellationToken.None);

        Assert.True(started.Elapsed < Operation);
        Assert.False(result.TreeUnavailable);
        Assert.Equal(1, fixture.Captured[0].Disposals);
    }

    // Partial: upstream's it.each is ['mask', 'screenshot']. The mask probes are locator counts the port gives no seam to hold, so only the
    // screenshot variant is ported; the mask variant is named in the PR report.
    [Fact]
    public async Task Partial_Keeps_a_complete_tree_when_optional_screenshot_capture_exhausts_its_deadline()
    {
        await using var fixture = await Fixture.StartAsync(null);
        fixture.HoldScreenshot = true;

        var snapshot = await fixture.Session.ObserveAsync(new ObserveOptions { Pixels = true }, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.False(snapshot.TreeUnavailable);
        Assert.Null(snapshot.Pixels);
        Assert.NotEmpty(snapshot.Roots);
        Assert.Equal(0, fixture.Elements.Sum(list => list.Disposals));
        fixture.Gate.SetResult();
        await Task.Delay(100);
    }

    [Fact]
    public async Task Runs_a_concurrent_observation_after_the_one_in_flight_and_publishes_both_in_call_order()
    {
        await using var fixture = await Fixture.StartAsync("metadata");

        var older = fixture.Session.ObserveAsync(null, Operation, CancellationToken.None);
        await Fixture.UntilAsync(() => fixture.Reads == 1);
        var newer = fixture.Session.ObserveAsync(null, Operation, CancellationToken.None);
        await Task.Delay(200);
        Assert.Equal(1, fixture.Reads);
        fixture.Gate.SetResult();

        Assert.Equal("about:blank", (await older).Location);
        Assert.Equal("about:blank", (await newer).Location);
        Assert.Equal(2, fixture.Reads);
        await Fixture.UntilAsync(() => fixture.Elements.Count == 2);
        Assert.Equal(1, fixture.Elements[0].Disposals);
        Assert.Equal(0, fixture.Elements[1].Disposals);
    }

    [Fact]
    public async Task Cannot_publish_a_capture_after_its_page_was_replaced()
    {
        await using var fixture = await Fixture.StartAsync("elements");

        var failure = Assert.ThrowsAsync<EngineException>(() => fixture.Session.ObserveAsync(null, Operation, CancellationToken.None));
        await Fixture.UntilAsync(() => fixture.Captured.Count == 1 && fixture.Captured[0].Held);
        await WebEngine.SurfaceOf(fixture.Session)!.Page().CloseAsync();
        await fixture.Session.OpenAsync("about:blank", CancellationToken.None);
        fixture.Gate.SetResult();

        Assert.Equal(EngineErrorCodes.NodeStale, (await failure).Code);
        await Fixture.UntilAsync(() => fixture.Elements.Count == 1 && fixture.Elements[0].Disposals == 1);
        Assert.Equal(1, fixture.Elements[0].Disposals);
    }

    /// <summary>A real engine over a button page, with the reader's reply and the screenshot observed and optionally held.</summary>
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string? _stalled;
        private int _reads;
        private int _screenshots;

        private Fixture(string? stalled) => _stalled = stalled;

        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<HeldHandle> Captured { get; } = [];

        public List<HeldHandle> Elements { get; } = [];

        public bool NeverDispose { get; set; }

        public bool HoldScreenshot { get; set; }

        public int Reads => Volatile.Read(ref _reads);

        public int Screenshots => Volatile.Read(ref _screenshots);

        public IEngineSession Session { get; private set; } = null!;

        public static async Task<Fixture> StartAsync(string? stalled)
        {
            var fixture = new Fixture(stalled);
            var engine = new WebEngine(headless: true)
            {
                ReadFrame = fixture.ReadAsync,
                CapturePage = fixture.CaptureAsync,
            };
            fixture.Session = await engine.StartAsync(new EngineStartOptions { ActionTimeout = Operation }, CancellationToken.None);
            await fixture.Session.OpenAsync("about:blank", CancellationToken.None);
            await WebEngine.SurfaceOf(fixture.Session)!.Page().SetContentAsync("<button>Button</button>");
            return fixture;
        }

        public static async Task UntilAsync(Func<bool> condition)
        {
            var deadline = Stopwatch.StartNew();
            while (!condition())
            {
                Assert.True(deadline.Elapsed < TimeSpan.FromSeconds(10), "the condition never held");
                await Task.Delay(20);
            }
        }

        public async ValueTask DisposeAsync()
        {
            Gate.TrySetResult();
            await Session.DisposeAsync();
        }

        public HeldHandle Track(IJSHandle inner)
        {
            var held = new HeldHandle(inner, null, Gate.Task, this, isCaptured: false);
            lock (Elements)
            {
                Elements.Add(held);
            }

            return held;
        }

        private async Task<IJSHandle> ReadAsync(IFrame frame, object arguments)
        {
            var first = Interlocked.Increment(ref _reads) == 1;
            var stalled = first ? _stalled : null;
            var real = await frame.EvaluateHandleAsync(PageScript.Collect, arguments);
            if (stalled == "evaluation")
            {
                await Gate.Task;
            }

            var held = new HeldHandle(real, stalled, Gate.Task, this, isCaptured: true);
            lock (Captured)
            {
                Captured.Add(held);
            }

            return held;
        }

        private async Task<byte[]> CaptureAsync(IPage page, PageScreenshotOptions options)
        {
            Interlocked.Increment(ref _screenshots);
            if (HoldScreenshot)
            {
                await Gate.Task;
            }

            return await page.ScreenshotAsync(options);
        }
    }

    /// <summary>
    /// The handle the reader returns (upstream's <c>captured</c>), or the in-page array of elements it holds (upstream's <c>elements</c>). Counts
    /// each call, holds the stage's reply once the real call has run, and never finishes disposal on request.
    /// </summary>
    private sealed class HeldHandle(IJSHandle inner, string? stalled, Task gate, Fixture owner, bool isCaptured) : IJSHandle
    {
        private int _disposals;
        private int _evaluations;
        private int _held;
        private int _properties;

        public int Disposals => Volatile.Read(ref _disposals);

        public int Evaluations => Volatile.Read(ref _evaluations);

        /// <summary>True once the real reply is in hand and held.</summary>
        public bool Held => Volatile.Read(ref _held) == 1;

        public int Properties => Volatile.Read(ref _properties);

        public async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposals);
            if (owner.NeverDispose)
            {
                await new TaskCompletionSource().Task;
            }

            await inner.DisposeAsync();
        }

        public async Task<T> EvaluateAsync<T>(string expression, object? arg = null)
        {
            Interlocked.Increment(ref _evaluations);
            var value = await inner.EvaluateAsync<T>(expression, arg);
            if (isCaptured && stalled == "metadata")
            {
                Volatile.Write(ref _held, 1);
                await gate;
            }

            return value;
        }

        public async Task<IJSHandle> GetPropertyAsync(string propertyName)
        {
            Interlocked.Increment(ref _properties);
            var property = await inner.GetPropertyAsync(propertyName);
            if (isCaptured && stalled == "elements")
            {
                Volatile.Write(ref _held, 1);
                await gate;
            }

            return isCaptured ? owner.Track(property) : property;
        }

        public Task<System.Text.Json.JsonElement?> EvaluateAsync(string expression, object? arg = null) => inner.EvaluateAsync(expression, arg);

        public Task<IJSHandle> EvaluateHandleAsync(string expression, object? arg = null) => inner.EvaluateHandleAsync(expression, arg);

        public Task<Dictionary<string, IJSHandle>> GetPropertiesAsync() => inner.GetPropertiesAsync();

        public Task<T> JsonValueAsync<T>() => inner.JsonValueAsync<T>();

        public IElementHandle? AsElement() => inner.AsElement();
    }
}
