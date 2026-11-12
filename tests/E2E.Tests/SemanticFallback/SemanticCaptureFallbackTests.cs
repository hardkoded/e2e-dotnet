// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Tests.AttemptSession;
using Microsoft.Playwright;

namespace E2E.Tests.SemanticFallback;

/// <summary>
/// Upstream's <c>describe('semantic capture fallback')</c> (<c>packages/web/tests/unit/semantic-fallback.test.ts</c>, tester-army/e2e at
/// <c>f185ca8</c>). Upstream opens an actual attempt on a fake browser whose independent mask probes can fail without a semantic reader, and
/// mocks <c>captureDocument</c>. The port opens a session on the same kind of fake (a <see cref="Fake{T}"/> browser, page and frame), and the
/// fake frame's <c>EvaluateHandleAsync</c> is the reader (upstream's <c>captureDocument</c>), the stand-in for the caret stylesheet's handle
/// otherwise.
/// </summary>
public sealed class SemanticCaptureFallbackTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private static readonly EngineException Timeout = new(EngineErrorCodes.OperationTimeout, "semantic reader timed out", retryable: false);

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task Takes_fresh_masked_pixels_after_a_timeout_retires_old_refs_and_reports_unavailable_semantics()
    {
        await using var setup = await SetupAsync();
        var disposed = 0;
        setup.Reader = () => Task.FromResult(setup.Document(["old"], () => Interlocked.Increment(ref disposed)));
        var first = await setup.Session.ObserveAsync(Budget, None);
        var old = Assert.Single(first.Roots);
        setup.Reader = () => Task.FromException<IJSHandle>(Timeout);

        var result = await setup.Session.ObserveAsync(new ObserveOptions { Pixels = true, PixelFallback = true }, Budget, None);

        Assert.True(result.TreeUnavailable);
        Assert.True(result.Truncated);
        Assert.Empty(result.Roots);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Pixels?.Png);
        Assert.Equal(2, setup.Screenshots.Count);
        Assert.Equal("#000000", setup.Screenshots[1].MaskColor);
        Assert.NotEmpty(setup.Screenshots[1].Mask!);
        Assert.Equal(1, disposed);
        var stale = await Assert.ThrowsAsync<EngineException>(() => setup.Session.PerformAsync(old, new LocatorAction.Tap(), None));
        Assert.Equal(EngineErrorCodes.NodeStale, stale.Code);
    }

    [Fact]
    public async Task Recovers_the_initial_tree_only_capture_when_the_caller_permits_pixels()
    {
        await using var setup = await SetupAsync();

        var result = await setup.Session.ObserveAsync(new ObserveOptions { PixelFallback = true }, Budget, None);

        Assert.True(result.TreeUnavailable);
        Assert.Single(setup.Screenshots);
    }

    [Fact]
    public async Task Fails_closed_when_a_mask_probe_cannot_prove_coverage()
    {
        await using var setup = await SetupAsync();
        setup.Count[0] = () => Task.FromException<int>(new InvalidOperationException("frame detached"));

        var error = await Assert.ThrowsAsync<EngineException>(() => setup.Session.ObserveAsync(new ObserveOptions { PixelFallback = true }, Budget, None));

        Assert.Same(Timeout, error);
    }

    [Fact]
    public async Task Does_not_turn_an_ordinary_tree_only_request_into_a_screenshot()
    {
        await using var setup = await SetupAsync();

        var error = await Assert.ThrowsAsync<EngineException>(() => setup.Session.ObserveAsync(Budget, None));

        Assert.Same(Timeout, error);
        Assert.Empty(setup.Screenshots);
    }

    [Fact]
    public async Task Preserves_the_fallback_reserve_when_a_concurrent_pixel_mask_probe_stalls()
    {
        await using var setup = await SetupAsync();
        var probes = 0;
        setup.Count[0] = () => Interlocked.Increment(ref probes) == 1 ? new TaskCompletionSource<int>().Task : Task.FromResult(1);
        // The reader waits out its deadline, as one that stalls does.
        setup.Reader = async () =>
        {
            await Task.Delay(Budget, None);
            throw Timeout;
        };

        var captured = await setup.Session.ObserveAsync(new ObserveOptions { Pixels = true, PixelFallback = true }, Budget, None);

        Assert.True(captured.TreeUnavailable);
        Assert.Single(setup.Screenshots);
    }

    [Fact]
    public async Task Preserves_a_hard_failure_from_the_fallback_screenshot()
    {
        await using var setup = await SetupAsync();
        var error = new TestException("POLICY_DENIED", "pixels denied");
        setup.Screenshot = _ => Task.FromException<byte[]>(error);

        var thrown = await Assert.ThrowsAsync<TestException>(() => setup.Session.ObserveAsync(new ObserveOptions { PixelFallback = true }, Budget, None));

        Assert.Same(error, thrown);
    }

    [Theory]
    [InlineData("POLICY_DENIED")]
    [InlineData("CANCELLED")]
    public async Task Preserves_code_instead_of_falling_back(string code)
    {
        await using var setup = await SetupAsync();
        Exception error = code == "POLICY_DENIED" ? new TestException(code, "stop") : new EngineException(code, "stop", retryable: false);
        setup.Reader = () => Task.FromException<IJSHandle>(error);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => setup.Session.ObserveAsync(new ObserveOptions { PixelFallback = true }, Budget, None));

        Assert.Same(error, thrown);
        Assert.Empty(setup.Screenshots);
    }

    [Fact]
    public async Task Preserves_cancellation_while_the_fallback_screenshot_is_in_flight()
    {
        await using var setup = await SetupAsync();
        using var controller = new CancellationTokenSource();
        setup.Screenshot = _ =>
        {
            controller.Cancel();
            return Task.FromResult(new byte[] { 1 });
        };

        var error = await Assert.ThrowsAsync<EngineException>(() => setup.Session.ObserveAsync(new ObserveOptions { PixelFallback = true }, Budget, controller.Token));

        Assert.Equal(EngineErrorCodes.Cancelled, error.Code);
    }

    /// <summary>Opens an actual attempt whose independent mask probes can fail without a semantic reader.</summary>
    internal static async Task<Setup> SetupAsync()
    {
        var setup = new Setup();
        try
        {
            await setup.OpenAsync();
            return setup;
        }
        catch
        {
            await setup.DisposeAsync();
            throw;
        }
    }

    internal sealed class Setup : IAsyncDisposable
    {
        /// <summary>The page's reader (upstream's <c>captureDocument</c> mock): rejected with the timeout until a test says otherwise.</summary>
        public Func<Task<IJSHandle>> Reader { get; set; } = () => Task.FromException<IJSHandle>(Timeout);

        /// <summary>The mask probes: the first covers the light DOM and open roots, the second the closed ones.</summary>
        public Func<Task<int>>[] Count { get; } = [() => Task.FromResult(1), () => Task.FromResult(0)];

        public Func<PageScreenshotOptions, Task<byte[]>> Screenshot { get; set; } = _ => Task.FromResult(new byte[] { 1, 2, 3 });

        public List<PageScreenshotOptions> Screenshots { get; } = [];

        /// <summary>How many screenshots the engine has started, read under the lock the page writes them under.</summary>
        public int ScreenshotCount()
        {
            lock (Screenshots)
            {
                return Screenshots.Count;
            }
        }

        public IEngineSession Session { get; private set; } = null!;

        public async Task OpenAsync()
        {
            var (frame, frameFake) = Fake<IFrame>.New();
            var (page, pageFake) = Fake<IPage>.New();
            frameFake.Members["get_Url"] = _ => "https://app.test/current";
            frameFake.Members["get_ParentFrame"] = _ => null;
            frameFake.Members["get_ChildFrames"] = _ => (IReadOnlyList<IFrame>)[];
            frameFake.Members["Locator"] = args =>
            {
                var (mask, maskFake) = Fake<ILocator>.New();
                var probe = ((string)args[0]!).StartsWith(PageScript.ClosedShadowSelectorEngine, StringComparison.Ordinal) ? 1 : 0;
                maskFake.Members["CountAsync"] = _ => Count[probe]();
                return mask;
            };
            frameFake.Members["EvaluateHandleAsync"] = args => (string)args[0]! == PageScript.Collect ? Reader() : Task.FromResult(NoCaret());
            pageFake.Members["get_Context"] = _ => null;
            pageFake.Members["get_IsClosed"] = _ => false;
            pageFake.Members["get_Url"] = _ => "https://app.test/current";
            pageFake.Members["get_MainFrame"] = _ => frame;
            pageFake.Members["get_Frames"] = _ => (IReadOnlyList<IFrame>)[frame];
            pageFake.Members["get_ViewportSize"] = _ => new PageViewportSizeResult { Width = 100, Height = 80 };
            pageFake.Members["SetDefaultTimeout"] = _ => null;
            pageFake.Members["SetViewportSizeAsync"] = _ => Task.CompletedTask;
            pageFake.Members["EmulateMediaAsync"] = _ => Task.CompletedTask;
            pageFake.Members["GotoAsync"] = _ => Task.FromResult<IResponse?>(null);
            pageFake.Members["CloseAsync"] = _ => Task.CompletedTask;
            pageFake.Members["ScreenshotAsync"] = args =>
            {
                var options = (PageScreenshotOptions)args[0]!;
                lock (Screenshots)
                {
                    Screenshots.Add(options);
                }

                return Screenshot(options);
            };

            var (context, contextFake) = Fake<IBrowserContext>.New();
            contextFake.Members["get_Pages"] = _ => (IReadOnlyList<IPage>)[page];
            contextFake.Members["NewPageAsync"] = _ => Task.FromResult(page);
            contextFake.Members["AddInitScriptAsync"] = _ => Task.FromResult(FakeRemote.Registration);
            contextFake.Members["SetDefaultTimeout"] = _ => null;
            contextFake.Members["CloseAsync"] = _ => Task.CompletedTask;
            var (browser, browserFake) = Fake<IBrowser>.New();
            browserFake.Members["NewContextAsync"] = _ => Task.FromResult(context);
            browserFake.Members["CloseAsync"] = _ => Task.CompletedTask;
            browserFake.Members["get_IsConnected"] = _ => true;

            var engine = new WebEngine(new WebEngineOptions
            {
                Viewport = new WebViewport(100, 80),
                Connect = new WebConnectOptions { CdpEndpoint = _ => Task.FromResult("provisioned") },
            })
            {
                CreatePlaywright = FakeRemote.Playwright,
                ConnectCdp = (_, _, _, _) => Task.FromResult(browser),
                RegisterCdpSelectors = (_, _, _) => Task.CompletedTask,
            };
            Session = await engine.StartAsync(new EngineStartOptions { ActionTimeout = Budget }, None);
            await Session.OpenAsync("https://app.test/current", None);
        }

        /// <summary>A document the reader read: its elements are one in-page list, and <paramref name="disposed"/> hears of its release.</summary>
        public IJSHandle Document(string[] ids, Action disposed)
        {
            var (elements, elementsFake) = Fake<IJSHandle>.New();
            elementsFake.Members["DisposeAsync"] = _ =>
            {
                disposed();
                return ValueTask.CompletedTask;
            };
            var (result, resultFake) = Fake<IJSHandle>.New();
            var roots = string.Join(",", ids.Select(id => $$"""{"ref":"{{id}}","role":"button"}"""));
            var metadata = JsonDocument.Parse($$"""{"next":{{ids.Length + 1}},"ids":[{{string.Join(",", ids.Select(id => $"\"{id}\""))}}],"count":{{ids.Length}},"roots":[{{roots}}]}""").RootElement.Clone();
            resultFake.Members["EvaluateAsync"] = _ => Task.FromResult(metadata);
            resultFake.Members["GetPropertyAsync"] = _ => Task.FromResult(elements);
            resultFake.Members["DisposeAsync"] = _ => ValueTask.CompletedTask;
            return result;
        }

        public async ValueTask DisposeAsync()
        {
            if (Session is not null)
            {
                await Session.DisposeAsync();
            }
        }

        // The handle the caret stylesheet's setup answers with: running it undoes nothing.
        private static IJSHandle NoCaret()
        {
            var (handle, fake) = Fake<IJSHandle>.New();
            fake.Members["EvaluateAsync"] = _ => Task.FromResult(default(JsonElement));
            fake.Members["DisposeAsync"] = _ => ValueTask.CompletedTask;
            return handle;
        }
    }
}
