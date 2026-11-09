// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using E2E.Engine;

namespace E2E.Tests.Evaluate;

/// <summary>
/// <c>browser.evaluate</c> keeps a page's own failure, which a test can fix, apart from
/// a failure of the browser, which it cannot.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class BrowserEvaluateErrorBoundariesTests
{
    private const string Hang = "async () => { await globalThis.__e2eReady(); await new Promise(() => { }); }";

    [Theory]
    [InlineData("""() => { throw new Error("cart is empty"); }""", "cart is empty")]
    [InlineData("""() => { throw "cart is empty"; }""", "cart is empty")]
    [InlineData("""() => { throw { message: "cart is empty" }; }""", "cart is empty")]
    [InlineData("() => { throw Object.create(null); }", "Page evaluation threw an unprintable value")]
    [InlineData("() => { throw null; }", "null")]
    [InlineData("""() => { throw new Error("page.evaluate: Target closed"); }""", "page.evaluate: Target closed")]
    public async Task Preserves_the_message_of_a_page_exception(string source, string message)
    {
        await AttemptAsync(async session =>
        {
            var error = await Assert.ThrowsAsync<TestException>(() => session.Browser.EvaluateAsync<object>(source));
            Assert.Equal("EVALUATE_FAILED", error.Code);
            Assert.Equal(message, error.Message);
        });
    }

    // Upstream mocks the rejection Playwright raises. The port has no such seam, so each row makes the browser raise it.
    [Theory]
    [InlineData("Target page, context or browser has been closed")]
    [InlineData("Execution context was destroyed, most likely because of a navigation.")]
    public async Task Keeps_a_Playwright_rejection_as_infrastructure(string rejection)
    {
        await AttemptAsync(async session =>
        {
            var error = rejection.StartsWith("Target", StringComparison.Ordinal)
                ? await EvaluateWhileAsync(session, () => WebEngine.SurfaceOf(session.Engine)!.Page().CloseAsync())
                : await Assert.ThrowsAsync<EngineException>(() => session.Browser.EvaluateAsync<object>("() => { location.href = 'about:blank'; return new Promise(() => { }); }"));
            Assert.Equal("ENGINE_FAILURE", error.Code);
            Assert.Contains(rejection, error.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Keeps_a_Playwright_timeout_as_OPERATION_TIMEOUT()
    {
        await AttemptAsync(async session =>
        {
            // Upstream calls the bare surface; a recorded step maps the engine error onto ACTION_FAILED.
            session.Browser.Steps = null;
            var error = await Assert.ThrowsAsync<EngineException>(() => session.Browser.EvaluateAsync<object>("() => new Promise(() => { })"));
            Assert.Equal("OPERATION_TIMEOUT", error.Code);
        });
    }

    [Theory]
    [InlineData("page")]
    [InlineData("context")]
    public async Task Keeps_a_real_closure_during_evaluation_as_infrastructure(string target)
    {
        await AttemptAsync(async session =>
        {
            var surface = WebEngine.SurfaceOf(session.Engine)!;
            var error = await EvaluateWhileAsync(session, () => target == "page" ? surface.Page().CloseAsync() : surface.Context().CloseAsync());
            Assert.Equal("ENGINE_FAILURE", error.Code);
        });
    }

    [Fact]
    public async Task Preserves_JSON_results_explicit_arguments_and_zero_argument_invocation()
    {
        await AttemptAsync(async session =>
        {
            Assert.Equal(new Counter(5), await session.Browser.EvaluateAsync<Counter>("value => ({ count: value.count + 1 })", new { count = 4 }));
            var data = await session.Browser.EvaluateAsync<Outcome>("async () => ({ success: false, message: \"ordinary data\" })");
            Assert.Equal(new Outcome(false, "ordinary data"), data);
            Assert.Equal(0, await session.Browser.EvaluateAsync<int>("function () { return arguments.length; }"));
            Assert.Equal(1, await session.Browser.EvaluateAsync<int>("function () { return arguments.length; }", null));
        });
    }

    [Fact]
    public async Task Evaluates_a_string_as_an_expression_and_calls_a_function_it_evaluates_to()
    {
        await AttemptAsync(async session =>
        {
            Assert.Equal("Cart", await session.Browser.EvaluateAsync<string>("document.title"));
            Assert.Equal("Cart", await session.Browser.EvaluateAsync<string>("(() => document.title)()"));
            Assert.Equal("ok", await session.Browser.EvaluateAsync<string>("""fetch("data:text/plain,ok").then((response) => response.text())"""));
            Assert.Equal("Cart", await session.Browser.EvaluateAsync<string>("(name) => document[name]", "title"));
        });
    }

    [Fact]
    public async Task Rejects_invalid_JSON_results_and_syntax_as_test_errors()
    {
        await AttemptAsync(async session =>
        {
            var infinity = await Assert.ThrowsAsync<TestException>(() => session.Browser.EvaluateAsync<double>("() => Infinity"));
            Assert.Equal("INVALID_ARGUMENT", infinity.Code);
            var syntax = await Assert.ThrowsAsync<TestException>(() => session.Browser.EvaluateAsync<object>("() => {"));
            Assert.Equal("EVALUATE_FAILED", syntax.Code);
            var withArgument = await Assert.ThrowsAsync<TestException>(() => session.Browser.EvaluateAsync<object>("() => {", null));
            Assert.Equal("EVALUATE_FAILED", withArgument.Code);
            await AssertInvalidAsync(session, "() => new Date()", "evaluate result must be JSON-safe");
            await AssertInvalidAsync(session, """() => { try { document.querySelector("!!"); } catch (error) { return error; } }""", "evaluate result must be JSON-safe");
            await AssertInvalidAsync(session, "() => ({ missing: undefined })", null);
            await AssertInvalidAsync(session, "() => { const cart = {}; cart.self = cart; return cart; }", "evaluate result contains a cycle");
        });
    }

    /// <summary>Partial: upstream also reads back a lone surrogate (<c>\ud800</c>) in a string result. <c>System.Text.Json</c> refuses to read one as a <c>string</c>, so the port cannot return it from <c>EvaluateAsync&lt;string&gt;</c> and the assertion is left out (#1456).</summary>
    [Fact]
    public async Task Partial_Returns_JSON_only()
    {
        await AttemptAsync(async session =>
        {
            await SetContentAsync(session, "<div></div>");
            await AssertInvalidAsync(session, """() => document.querySelector("div").getBoundingClientRect()""", "evaluate result must be JSON-safe");
            await AssertInvalidAsync(session, "() => document.body", null);
            var box = await session.Browser.EvaluateAsync<Dictionary<string, double>>("""() => document.querySelector("div").getBoundingClientRect().toJSON()""");
            Assert.True(box!.ContainsKey("width"));
            Assert.Equal(
                [new Item("a"), new Item("a")],
                (await session.Browser.EvaluateAsync<Item[]>("""() => { const item = { sku: "a" }; return [item, item]; }"""))!);
            Assert.Equal(
                "quote \" line\n\u2028 \ud83d\ude00",
                await session.Browser.EvaluateAsync<string>("""() => "quote \" line\n\u2028 \ud83d\ude00" """));
        });
    }

    [Fact]
    public async Task Returns_a_result_after_the_page_deletes_or_replaces_the_globals_the_transport_could_use()
    {
        await AttemptAsync(async session =>
        {
            var result = await session.Browser.EvaluateAsync<Basket>(
                "() => { delete window.Array; window.Map = 1; window.JSON = 1; window.Object = 1; window.String = 1; return { items: [1, \"two\"], ok: true }; }");
            Assert.True(result!.Ok);
            Assert.Equal(2, result.Items.Length);
        });
    }

    [Fact]
    public async Task Fails_a_later_evaluate_and_a_locate_as_EVALUATE_FAILED_once_the_page_replaced_Map()
    {
        await AttemptAsync(async session =>
        {
            await SetContentAsync(session, "<div>cart</div><script>window.Map = 1;</script>");
            var evaluate = await Assert.ThrowsAsync<TestException>(() => session.Browser.EvaluateAsync<int>("() => 1"));
            Assert.Equal("EVALUATE_FAILED", evaluate.Code);
            var locate = await Assert.ThrowsAsync<TestException>(() => session.Browser.Locator("div").CountAsync());
            Assert.Equal("EVALUATE_FAILED", locate.Code);
        });
    }

    [Fact]
    public async Task Keeps_returning_results_on_a_page_that_deleted_Array_and_JSON_and_hid___proto__()
    {
        await AttemptAsync(async session =>
        {
            await SetContentAsync(session, "<script>delete window.Array; delete window.JSON; delete Object.prototype.__proto__;</script>");
            var basket = await session.Browser.EvaluateAsync<Basket>("(n) => ({ items: [n, 'two'], ok: true })", 1);
            Assert.Equal(2, basket!.Items.Length);
            var pair = await session.Browser.EvaluateAsync<int[]>("() => [1, 2]");
            Assert.Equal(new[] { 1, 2 }, pair);
        });
    }

    private static Task SetContentAsync(E2ESession session, string html) =>
        WebEngine.SurfaceOf(session.Engine)!.Page().SetContentAsync(html);

    /// <summary>Evaluates <paramref name="source"/> and expects <c>INVALID_ARGUMENT</c>, with <paramref name="message"/> when given.</summary>
    private static async Task AssertInvalidAsync(E2ESession session, string source, string? message)
    {
        var error = await Assert.ThrowsAsync<TestException>(() => session.Browser.EvaluateAsync<object>(source));
        Assert.Equal("INVALID_ARGUMENT", error.Code);
        if (message is not null)
        {
            Assert.Equal(message, error.Message);
        }
    }

    /// <summary>Starts a script that never ends, waits until it runs, then lets <paramref name="close"/> take the browser from under it.</summary>
    private static async Task<E2EException> EvaluateWhileAsync(E2ESession session, Func<Task> close)
    {
        var started = new TaskCompletionSource();
        await WebEngine.SurfaceOf(session.Engine)!.Page().ExposeFunctionAsync("__e2eReady", () => started.TrySetResult());
        var pending = session.Browser.EvaluateAsync<object>(Hang);
        await started.Task;
        await close();
        return await Assert.ThrowsAnyAsync<E2EException>(() => pending);
    }

    private static async Task AttemptAsync(Func<E2ESession, Task> body)
    {
        using var site = await TinySite.StartAsync("<!doctype html><title>Cart</title><h1>Cart</h1>");
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(new WebEngineOptions { Headless = true }),
            BaseUrl = site.Url,
            TestTitle = "browser.evaluate error boundaries",
            AssertionTimeout = TimeSpan.FromSeconds(5),
            ActionTimeout = TimeSpan.FromSeconds(1),
            StepTimeout = TimeSpan.FromSeconds(20),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
        await using (session)
        {
            Exception? error = null;
            try
            {
                await session.App.OpenAsync("/");
                await body(session);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            session.Complete(error);
            Assert.Null(error);
        }
    }

    private sealed record Item(string Sku);

    private sealed record Basket(object[] Items, bool Ok);

    private sealed record Counter(int Count);

    private sealed record Outcome(bool Success, string Message);
}
