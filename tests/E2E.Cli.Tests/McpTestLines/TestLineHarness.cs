// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Cli.Mcp;
using E2E.Engine;

namespace E2E.Cli.Tests.McpTestLines;

/// <summary>
/// One node on the fake screen, as upstream's <c>Fake</c>: what observe lists and what the live look reads. Every node is 100 by 20
/// at x 0, so no two boxes overlap.
/// </summary>
internal sealed record Fake(string Id, string? Role = null, string? Name = null, string? Text = null, string? TestId = null, string? Placeholder = null, double Y = 0, bool Hidden = false, params Fake[] Children)
{
    public SemanticNode ToNode() => new()
    {
        Ref = Id,
        Role = Role,
        Name = Name,
        Text = Text,
        TestId = TestId,
        Placeholder = Placeholder,
        States = new NodeStates { Hidden = Hidden },
        Rect = new BoundingBox(0, Y, 100, 20),
        Children = [.. Children.Select(child => child.ToNode())],
    };
}

/// <summary>
/// A session's test lines over one screen: the nodes the newest observation listed and the nodes the live look reads, resolved by the
/// port's own locator resolver, as upstream's <c>harness</c> resolves them from a table of what each locator matches.
/// </summary>
internal sealed class TestLineHarness
{
    private int _resolves;

    public TestLineHarness(IReadOnlyList<Fake> screen, IReadOnlyList<Fake>? live = null, bool observed = true)
    {
        var listed = new Fake("root", "document", "Page", Children: [.. screen]).ToNode();
        var seen = new Observation { Route = "/", Roots = [new Fake("root", "document", "Page", Children: [.. live ?? screen]).ToNode()] };
        Lines = new SessionTestLines(new TestLineOptions
        {
            Newest = () => observed ? [listed] : null,
            Redact = text => text.Replace("sk-live-0000", "<secret:apiKey>", StringComparison.Ordinal),
            AppBase = new Uri("http://127.0.0.1:4100/"),
            Secrets = ["apiKey"],
            Resolve = (expression, _) =>
            {
                _resolves++;
                return Task.FromResult(LocatorResolver.Resolve(seen, expression));
            },
        });
    }

    public SessionTestLines Lines { get; }

    /// <summary>How many locators the session tried on the live screen.</summary>
    public int Resolves => _resolves;

    /// <summary>Runs one grammar tool through the session's wrapping and returns its text.</summary>
    public async Task<string> RunAsync(string tool, string args = "{}", Func<Task<string>>? action = null)
    {
        var inner = new CatalogTool
        {
            InputSchema = JsonDocument.Parse("{}").RootElement,
            Execute = async (_, _) => action is null ? "done #" + tool + "\n\nScreen changes" : await action(),
        };
        var wrapped = Lines.Wrap(tool, inner);
        return (string)(await wrapped.Execute!(JsonDocument.Parse(args).RootElement, CancellationToken.None))!;
    }

    /// <summary>The line a result carries after its lead, or why it has none.</summary>
    public static string NoteOf(string text) => text.Split('\n')[1];
}
