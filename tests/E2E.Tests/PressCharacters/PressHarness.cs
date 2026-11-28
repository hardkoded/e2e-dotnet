// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.PressCharacters;

/// <summary>
/// Upstream's <c>start</c> and <c>open</c> of <c>press-characters.test.ts</c>: one web engine on a page that logs the key events it gets.
/// </summary>
internal sealed class PressHarness : IAsyncDisposable
{
    private const string KeyLog = """
        <script>
          const row = (event) => [event.type, event.key ?? event.data, ...(event.ctrlKey ? ['ctrl'] : []), ...(event.shiftKey ? ['shift'] : [])].join(':');
          for (const type of ['keydown', 'input', 'keyup']) {
            document.addEventListener(type, (event) => { document.querySelector('[data-testid=keys]').value += row(event) + ';'; }, true);
          }
        </script>
        """;

    private const string Frame =
        "<input data-testid='inner'><script>addEventListener('keydown', (event) => { document.title += event.key; })</script>";

    private static readonly string Page = $"""
        <!doctype html><meta charset="utf-8">
        <input data-testid="open">
        <label>Name <input></label>
        <textarea data-testid="keys" readonly></textarea>
        <div data-testid="rich" contenteditable="true"></div>
        <input data-testid="count" type="number">
        <button data-testid="go">Go</button>
        <iframe srcdoc="{Frame.Replace("\"", "&quot;")}"></iframe>
        {KeyLog}
        """;

    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly IEngineSession _session;
    private readonly string _artifactsDir;

    private PressHarness(IEngineSession session, string artifactsDir)
    {
        _session = session;
        _artifactsDir = artifactsDir;
    }

    public static readonly LocatorExpression Open = By("testid", "open");

    /// <summary>A label query pins an element handle, where a test id keeps a locator: the two targets <c>press</c> acts on.</summary>
    public static readonly LocatorExpression Labelled = By("label", "Name");

    public static readonly LocatorExpression Go = By("testid", "go");
    public static readonly LocatorExpression Keys = By("testid", "keys");
    public static readonly LocatorExpression Rich = By("testid", "rich");
    public static readonly LocatorExpression Count = By("testid", "count");

    public static readonly LocatorExpression Inner = new()
    {
        Kind = "frame",
        Selector = "iframe",
        Source = By("testid", "inner"),
    };

    public static async Task<PressHarness> StartAsync(BrowserName browser)
    {
        var artifactsDir = Directory.CreateTempSubdirectory("e2e-press-").FullName;
        try
        {
            var session = await new WebEngine(
                new WebEngineOptions { Headless = true, BrowserName = browser }
            ).StartAsync(new EngineStartOptions { ArtifactsDir = artifactsDir, ActionTimeout = Timeout }, None);
            return new PressHarness(session, artifactsDir);
        }
        catch
        {
            Directory.Delete(artifactsDir, recursive: true);
            throw;
        }
    }

    public Task OpenPageAsync() =>
        _session.OpenAsync($"data:text/html;charset=utf-8,{Uri.EscapeDataString(Page)}", None);

    public async Task PressAsync(LocatorExpression expression, string key) =>
        await _session.PerformAsync(await OneAsync(expression), new LocatorAction.Press(key), None);

    public async Task PressFocusedAsync(string key)
    {
        await _session.ObserveAsync(None);
        await _session.PressAsync(key, None);
    }

    public async Task<string?> ValueOfAsync(LocatorExpression expression) => (await OneAsync(expression)).Value;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _session.DisposeAsync();
        }
        finally
        {
            Directory.Delete(_artifactsDir, recursive: true);
        }
    }

    private static LocatorExpression By(string kind, string value) =>
        LocatorExpression.For(kind, value, new TextMatchOptions { Exact = true }, nameof(value));

    private async Task<SemanticNode> OneAsync(LocatorExpression expression)
    {
        var nodes = await _session.LocateAsync(expression, Timeout, None);
        return nodes.Count > 0 ? nodes[0] : throw new InvalidOperationException($"no node for {expression.Kind}");
    }
}
