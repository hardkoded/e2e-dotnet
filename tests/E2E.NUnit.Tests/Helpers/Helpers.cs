// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace E2E.NUnit.Tests;

/// <summary>
/// The helpers of the tests ported from Playwright, upstream's <c>apps/testbed/tests/helpers.ts</c>:
/// <c>content</c> and <c>PREFIX</c>. <see cref="ExpectedToFailAsync"/> is .NET-only (see its summary).
/// </summary>
internal static class Helpers
{
    /// <summary>The app's origin, Playwright's <c>server.PREFIX</c> in the tests ported from it.</summary>
    public static string PREFIX => E2E.Playground.Testbed.Url;

    /// <summary>Another origin on the same app, Playwright's <c>server.CROSS_PROCESS_PREFIX</c>.</summary>
    public static string CROSS => E2E.Playground.Testbed.Url.Replace("127.0.0.1", "localhost", StringComparison.Ordinal);

    /// <summary>A path that serves <paramref name="html"/> as a page, for Playwright's <c>page.setContent(html)</c>.</summary>
    public static string Content(string html) =>
        "/content?html=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(html)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// An equality check for a body run by <see cref="ExpectedToFailAsync"/>. It throws: an NUnit assertion inside a test body is
    /// recorded as a failure of the test even when the body catches it.
    /// </summary>
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, but was {actual}.");
        }
    }

    /// <summary>A check that <paramref name="actual"/> contains <paramref name="expected"/>, for a body run by <see cref="ExpectedToFailAsync"/>. It throws, as <see cref="Equal{T}"/> does.</summary>
    public static void Contain(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected the text to contain {expected}, but was {actual}.");
        }
    }

    /// <summary>The absolute path of one of Playwright's test assets.</summary>
    public static string Asset(string name) => Path.Combine(AppContext.BaseDirectory, "Testbed", "playwright", "assets", name);

    /// <summary>A promise with its resolver, for a route handler the test holds open. .NET-only, a test helper.</summary>
    public static TaskCompletionSource<T> Deferred<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>A <see cref="Deferred{T}"/> with no value.</summary>
    public static TaskCompletionSource Deferred() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The request headers with <paramref name="set"/> added or replaced, for <c>{ ...route.request.headers, name: value }</c>. .NET-only, a test helper.</summary>
    public static Dictionary<string, string> WithHeaders(IReadOnlyDictionary<string, string> headers, params (string Name, string Value)[] set)
    {
        var copy = new Dictionary<string, string>(headers, StringComparer.Ordinal);
        foreach (var (name, value) in set)
        {
            copy[name] = value;
        }

        return copy;
    }

    /// <summary>The request the server received, which <c>/cors-echo</c> and <c>/echo</c> answer with. .NET-only, a test helper.</summary>
    public sealed record Served(string Method, Dictionary<string, string> Headers, string Body, string Url)
    {
        /// <summary>The header named <paramref name="name"/>, or <see langword="null"/> when the request had none.</summary>
        public string? Header(string name) => Headers.GetValueOrDefault(name);

        public static Served Parse(string json) =>
            System.Text.Json.JsonSerializer.Deserialize<Served>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
    }

    /// <summary>
    /// Playwright's <c>Promise.all([wait, action])</c> for a wait whose result the test reads: waits for both and returns the result of <paramref name="wait"/>.
    /// .NET-only, a test helper.
    /// </summary>
    public static async Task<T> AllAsync<T>(Task<T> wait, Task action)
    {
        var first = await Task.WhenAny(wait, action).ConfigureAwait(false);
        if (first.IsFaulted)
        {
            await first.ConfigureAwait(false);
        }

        await Task.WhenAll(wait, action).ConfigureAwait(false);
        return wait.Result;
    }

    /// <summary>
    /// Runs the body of a test upstream marks <c>{ fail: 'reason' }</c>, a known difference from Playwright: the test passes while
    /// <paramref name="body"/> still fails with a message that contains <paramref name="failure"/>, and fails once it passes, so the marker is removed. The port has no <c>fail</c> test option
    /// (see COMPATIBILITY.md), and NUnit has no expected-failure outcome.
    /// </summary>
    public static async Task ExpectedToFailAsync(string failure, Func<Task> body)
    {
        try
        {
            await body().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Assert.That(ex.Message, Does.Contain(failure), "the body failed, but not with the known difference");
            return;
        }

        Assert.Fail("UNEXPECTED_PASS: the test is marked as a known difference from Playwright, and it passed. Remove the marker.");
    }
}

/// <summary>
/// The base of the classes ported from Playwright's page tests: the playground as the base URL, the cache off, and the <c>playwright</c>
/// category (upstream's <c>tags: ['playwright']</c>).
/// </summary>
[Category("playwright")]
public abstract class PlaywrightTest : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;
}
