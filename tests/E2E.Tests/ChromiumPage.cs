// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests;

/// <summary>
/// One plain Playwright page per test class, as upstream's <c>beforeAll</c> launches Chromium and opens a page
/// for a test file. The port's reader, <see cref="PageScript.Collect"/>, runs on this same page, so each check
/// compares it with what Playwright itself answers there.
/// </summary>
public sealed class ChromiumPage : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private int _nextRef = 1;

    public IPage Page { get; private set; } = null!;

    /// <summary>Whether the last <see cref="CaptureAsync"/> stopped at its node budget.</summary>
    public bool Truncated { get; private set; }

    /// <summary>How many nodes the last <see cref="CaptureAsync"/> counted against its budget.</summary>
    public int NodeCount { get; private set; }

    public async Task InitializeAsync()
    {
        await WebEngine.EnsureChromiumAsync(headed: false, CancellationToken.None);
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        Page = await _browser.NewPageAsync(
            new BrowserNewPageOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
            }
        );
        // The init script that records closed roots runs on a navigation; setContent alone is none.
        await Page.AddInitScriptAsync(PageScript.RecordClosedShadowRoots);
        await Page.GotoAsync("about:blank");
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }

    /// <summary>
    /// The port's reader over the page: every node it lists, in document order. <paramref name="budget"/> is the
    /// most nodes the walk counts, as upstream's capture takes it.
    /// </summary>
    public async Task<IReadOnlyList<ReadNode>> CaptureAsync(int budget = ObservationLimits.Nodes)
    {
        // The next free ref carries over, as the engine's does, so a ref never names two elements.
        var observed = await Page.EvaluateAsync<JsonElement>(
            PageScript.Collect,
            new
            {
                seed = _nextRef,
                max = budget,
                testIdAttribute = "data-testid",
            }
        );
        _nextRef = observed.GetProperty("next").GetInt32();
        Truncated = observed.GetProperty("truncated").GetBoolean();
        NodeCount = observed.GetProperty("count").GetInt32();
        var nodes = new List<ReadNode>();
        Flatten(observed.GetProperty("roots"), nodes);
        return nodes;
    }

    /// <summary>
    /// The reader's node for the element <paramref name="locator"/> names, read in node mode on that element alone, as upstream's
    /// <c>readSemanticsFunction</c> is evaluated on a locator: the element's own hidden state reads up the flat tree, and a hidden
    /// element has a read though the tree leaves it out.
    /// </summary>
    public async Task<ReadNode?> ReadAsync(ILocator locator)
    {
        var read = await locator.EvaluateAsync<JsonElement?>(NodeRead, "data-testid");
        return read is { ValueKind: JsonValueKind.Object } raw ? ToReadNode(raw, "") : null;
    }

    private static readonly string NodeRead = $$"""
        (el, testIdAttribute) => {
        {{ReadSemantics.Prelude}}
        {{ReadSemantics.NodeMode}}
          return nodeOf(el, testIdAttribute);
        }
        """;

    private static void Flatten(JsonElement list, List<ReadNode> into)
    {
        foreach (var node in list.EnumerateArray())
        {
            into.Add(ToReadNode(node, node.GetProperty("ref").GetString()!));
            Flatten(node.GetProperty("children"), into);
        }
    }

    private static ReadNode ToReadNode(JsonElement node, string reference)
    {
        var rect = node.GetProperty("rect");
        return new ReadNode(
            reference,
            StringOf(node, "role"),
            StringOf(node, "name"),
            StringOf(node, "text"),
            StringOf(node, "value"),
            StringOf(node, "selection"),
            StringOf(node, "testId"),
            node.GetProperty("focused").GetBoolean(),
            node.GetProperty("secure").GetBoolean(),
            node.GetProperty("hidden").GetBoolean(),
            node.GetProperty("ariaHidden").GetBoolean(),
            node.GetProperty("selected").GetBoolean(),
            node.GetProperty("checked").GetBoolean(),
            node.GetProperty("disabled").GetBoolean(),
            node.GetProperty("attributes")
                .EnumerateObject()
                .ToDictionary(
                    attribute => attribute.Name,
                    attribute => attribute.Value.GetString() ?? "",
                    StringComparer.Ordinal
                ),
            rect.ValueKind == JsonValueKind.Null
                ? null
                : new BoundingBox(
                    rect.GetProperty("x").GetDouble(),
                    rect.GetProperty("y").GetDouble(),
                    rect.GetProperty("width").GetDouble(),
                    rect.GetProperty("height").GetDouble()
                )
        );
    }

    private static string? StringOf(JsonElement node, string name)
    {
        return node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}

/// <summary>One node as the port's reader reports it.</summary>
public sealed record ReadNode(
    string Ref,
    string? Role,
    string? Name,
    string? Text,
    string? Value,
    string? Selection,
    string? TestId,
    bool Focused,
    bool Secure,
    bool Hidden,
    bool AriaHidden,
    bool Selected,
    bool Checked,
    bool Disabled,
    IReadOnlyDictionary<string, string> Attributes,
    BoundingBox? Rect
)
{
    /// <summary>The node as an observation carries it, for what reads an observation (the snapshot).</summary>
    public SemanticNode ToSemanticNode() =>
        new()
        {
            Ref = Ref,
            Role = Role,
            Name = Name,
            Text = Text,
            Value = Value,
            TestId = TestId,
            States = new NodeStates { Hidden = Hidden, AriaHidden = AriaHidden },
            Attributes = Attributes,
            Rect = Rect,
        };
}
