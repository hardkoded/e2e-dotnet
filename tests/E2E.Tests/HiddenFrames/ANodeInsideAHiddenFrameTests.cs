// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.HiddenFrames;

/// <summary>
/// Upstream's <c>describe('a node inside a hidden frame')</c> (<c>packages/web/tests/integration/hidden-frames.test.ts</c>, tester-army/e2e#1549,
/// <c>08b102a</c>): a node inside a hidden frame reads as hidden. The frame element's own hidden state, read in the document around it,
/// counts for every node in its document and in the frames nested under it. A <c>visible</c> or role query there matches nothing, under an
/// index too, as neither matches a hidden node.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ANodeInsideAHiddenFrameTests : IAsyncLifetime
{
    private const string Document = """
        <iframe id="hidden" style="visibility: hidden" srcdoc="<button>Hidden</button>"></iframe>
        <div style="visibility: hidden">
          <iframe id="hidden-parent" srcdoc="<button>Hidden parent</button>"></iframe>
          <iframe id="override" style="visibility: visible" srcdoc="<button>Override</button>"></iframe>
        </div>
        <iframe id="outer" style="visibility: hidden" srcdoc="<iframe id='inner' srcdoc='<button>Nested</button>'></iframe>"></iframe>
        """;

    private static readonly LocatorExpression Button = new() { Kind = "selector", Selector = "button" };

    private static readonly LocatorExpression RoleButton = new() { Kind = "role", Role = "button" };

    private static readonly LocatorExpression VisibleButton = new() { Kind = "role", Role = "button", Visible = true };

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private IEngineSession _session = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions(), CancellationToken.None);
        await _session.OpenAsync("about:blank", CancellationToken.None);
        _page = WebEngine.SurfaceOf(_session)!.Page();
        await _page.SetContentAsync(Document);
        await _page.FrameLocator("#outer").FrameLocator("#inner").GetByRole(AriaRole.Button).WaitForAsync(new() { State = WaitForSelectorState.Attached });
    }

    public async Task DisposeAsync() => await _session.DisposeAsync();

    /// <summary>Wraps <paramref name="source"/> in <c>frame</c> scopes, outermost first, as a <c>Browser.FrameLocator</c> chain does.</summary>
    private static LocatorExpression WithinFrames(string[] selectors, LocatorExpression source) =>
        selectors.Reverse().Aggregate(source, (inner, selector) => new LocatorExpression { Kind = "frame", Selector = selector, Source = inner });

    private Task<IReadOnlyList<SemanticNode>> LocateAsync(string[] frames, LocatorExpression source) =>
        _session.LocateAsync(WithinFrames(frames, source), Timeout, CancellationToken.None);

    [Fact]
    public async Task Reads_as_hidden_when_its_frame_an_ancestor_of_the_frame_or_an_enclosing_frame_is_hidden()
    {
        foreach (var frames in new[] { new[] { "#hidden" }, ["#hidden-parent"], ["#outer", "#inner"] })
        {
            var match = Assert.Single(await LocateAsync(frames, Button));
            Assert.True(match.States.Hidden);
        }
    }

    [Fact]
    public async Task Reads_as_shown_in_a_frame_that_shows_itself_again_under_a_hidden_ancestor()
    {
        var match = Assert.Single(await LocateAsync(["#override"], Button));
        Assert.False(match.States.Hidden);
    }

    [Fact]
    public async Task Is_no_match_of_a_visible_or_role_query_indexed_or_not()
    {
        // An index is taken outside the frame scope, as Browser.FrameLocator(...).GetByRole(...).First() builds it.
        var expressions = new Func<string, LocatorExpression>[]
        {
            frame => WithinFrames([frame], VisibleButton),
            frame => new LocatorExpression { Kind = "index", Source = WithinFrames([frame], VisibleButton), Index = 0 },
            frame => WithinFrames([frame], RoleButton),
        };
        foreach (var expression in expressions)
        {
            Assert.Empty(await _session.LocateAsync(expression("#hidden"), Timeout, CancellationToken.None));
            Assert.Single(await _session.LocateAsync(expression("#override"), Timeout, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Reads_as_shown_once_the_frame_shows_again()
    {
        await _page.Locator("#hidden").EvaluateAsync("frame => { frame.style.visibility = 'visible'; }");

        var match = Assert.Single(await LocateAsync(["#hidden"], Button));
        Assert.False(match.States.Hidden);
    }
}
