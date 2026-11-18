// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.Crosscheck;

/// <summary>One field on which the engine and an oracle read a node differently (upstream's <c>Disagreement</c>).</summary>
/// <param name="Oracle"><c>chrome</c>, <c>playwright</c>, or <c>axe</c>.</param>
/// <param name="Field"><c>role</c>, <c>name</c>, a state name, <c>level</c>, <c>missing</c> (Chrome has the node, the engine does not), or <c>unknown</c> (the other way).</param>
/// <param name="Ours">The engine's value.</param>
/// <param name="Theirs">The oracle's value.</param>
/// <param name="Node"><c>role "name"</c> as the engine reads it, or as Chrome does for a node the engine left out.</param>
public sealed record Disagreement(string Oracle, string Field, string Ours, string Theirs, string Node);

/// <summary>The outcome of one check (upstream's <c>CrossCheckResult</c>).</summary>
/// <param name="Compared">Engine nodes compared against at least one oracle; zero means the page checked nothing.</param>
/// <param name="Disagreements">Every field on which an oracle read a node differently.</param>
public sealed record CrossCheckResult(int Compared, IReadOnlyList<Disagreement> Disagreements);

/// <summary>One node of the engine's tree, as the cross-check reads it (upstream's <c>SemanticNode</c>, the fields compared).</summary>
public sealed record CheckNode(
    string Id,
    string? Role,
    string? Name,
    int? Level,
    bool Hidden,
    bool AriaHidden,
    IReadOnlyDictionary<string, bool> States
);

/// <summary>Where an element sits, for the policies below and the oracles' reach (upstream's <c>ElementFacts</c>).</summary>
/// <param name="InChildFrame">Neither oracle reads a child frame from the top page.</param>
/// <param name="InClosedShadow">Playwright cannot pierce a closed root; the engine records them at creation.</param>
/// <param name="EditingHost">An editor's host, which the tree reads as the <c>textbox</c> it is. Chrome calls it <c>generic</c>.</param>
/// <param name="SvgRoot">An inline <c>&lt;svg&gt;</c>, which Chrome ignores unless something names it.</param>
public sealed record ElementFacts(bool InChildFrame, bool InClosedShadow, bool EditingHost, bool SvgRoot);

/// <summary>One capture through the engine's own reader, with the facts of the element behind each node id.</summary>
public sealed record TaggedCapture(IReadOnlyList<CheckNode> Nodes, IReadOnlyDictionary<string, ElementFacts> Facts);

/// <summary>
/// Differential check of the engine's semantic tree against two oracles on the same live page: Chrome's own accessibility tree
/// over CDP, and Playwright's <c>getByRole</c> resolution (upstream's <c>crosscheck.ts</c>). Every node the engine reports is
/// tagged in the DOM, then looked up in both; every interactive node Chrome exposes that the engine left out is reported too.
/// The result is a list of disagreements keyed without node ids, so a list can be committed as the expected state of a page and
/// diffed on the next run (<see cref="CrosscheckExpectations"/>).
/// </summary>
public static partial class Crosscheck
{
    /// <summary>The attribute each published element carries its node id in.</summary>
    public const string Marker = "data-e2e-crosscheck";

    /// <summary>Engine roles never compared: structure and text the tree keeps for the model, with no ARIA counterpart.</summary>
    private static readonly HashSet<string> StructuralRoles = ["document", "text", "generic", "none", "presentation"];

    /// <summary>The vocabulary spells a few ARIA roles its own way.</summary>
    private static readonly Dictionary<string, string> AriaRole = new() { ["image"] = "img" };

    // Deliberate differences. Each is a choice the tree makes for the model, kept here with its reason rather than repeated per
    // page in expected.txt, so the file lists only what nobody has decided yet.

    /// <summary>
    /// Roles the tree names from content where accname, Chrome, and Playwright leave them unnamed (upstream's <c>NAME_FROM_CONTENT_ROLES</c>
    /// in <c>read-semantics.ts</c>): a list row or a status message is read by what it says.
    /// </summary>
    private static readonly HashSet<string> EngineNamedRoles = ["listitem", "status", "alert"];

    /// <summary>
    /// Roles accname or HTML-AAM names (a row from content, a figure by its figcaption) where Chrome's tree leaves the name empty.
    /// Playwright agrees with the engine on these.
    /// </summary>
    private static readonly HashSet<string> ChromeUnnamedRoles = ["row", "figure"];

    /// <summary>
    /// Chrome's own role names where the vocabulary uses ARIA's, or where ARIA has none: a date or colour input reads as
    /// <c>textbox</c> to the engine and to Playwright.
    /// </summary>
    private static readonly Dictionary<string, string[]> ChromeRoles = new()
    {
        ["image"] = ["image"],
        ["iframe"] = ["Iframe"],
        ["textbox"] = ["textbox", "Date", "DateTime", "InputTime", "ColorWell"],
    };

    /// <summary>Engine roles Chrome may ignore: a row group keeps a table's controls in their section.</summary>
    private static readonly HashSet<string> ChromeIgnores = ["rowgroup"];

    /// <summary>Engine roles with no Chrome counterpart: <c>box</c> is a visible empty element the tree lists so a drag or a click has something to aim at.</summary>
    private static readonly HashSet<string> EngineOnlyRoles = ["box"];

    /// <summary>Engine roles ARIA does not define, so <c>getByRole</c> cannot be asked for them.</summary>
    private static readonly HashSet<string> NonAriaRoles = ["box", "iframe"];

    /// <summary>Chrome roles the engine is expected to report whenever Chrome exposes them.</summary>
    private static readonly HashSet<string> InteractiveChromeRoles =
    [
        "button",
        "link",
        "textbox",
        "searchbox",
        "checkbox",
        "radio",
        "switch",
        "combobox",
        "listbox",
        "option",
        "slider",
        "spinbutton",
        "tab",
        "menuitem",
        "menuitemcheckbox",
        "menuitemradio",
        "treeitem",
    ];

    /// <summary>The ARIA states compared, as Chrome's AX property names.</summary>
    private static readonly string[] States = ["checked", "disabled", "expanded", "selected", "pressed"];

    // An icon-font glyph is a private-use code point (upstream's ICON_GLYPHS in read-semantics.ts and glyphTolerantName in
    // locators.ts). The pattern Playwright is given is JavaScript's and runs without the `u` flag, so the plane-15 and plane-16
    // code points are written as surrogate pairs. .NET-only.
    private const string GlyphPattern = @"(?:[-]|[\uDB80-\uDBFF][\uDC00-\uDFFF])";

    private const string TagScript = """
        ([attribute]) => {
          const elements = window[Symbol.for("e2e.observation.elements")];
          const facts = {};
          for (const [id, el] of elements) {
            if (el.nodeType !== 1) continue;
            el.setAttribute(attribute, id);
            const root = el.getRootNode();
            const view = el.ownerDocument.defaultView;
            facts[id] = {
              inChildFrame: view !== null && view !== view.top,
              inClosedShadow: root instanceof ShadowRoot && root.mode === "closed",
              editingHost: el instanceof HTMLElement && el.isContentEditable,
              svgRoot: el instanceof SVGSVGElement,
            };
          }
          return facts;
        }
        """;

    // A node inside a form control's own shadow tree (a date input's day field) is the browser's, not the page's: reported as
    // tagged with no id so it never counts as missing.
    private const string MarkerOfScript = """
        function (marker) {
          let node = this;
          while (node !== null && !(node instanceof ShadowRoot)) node = node.parentNode;
          if (node !== null && node.host.matches('input, select, textarea, video, audio')) return '';
          return this.nodeType === 1 ? this.getAttribute(marker) : null;
        }
        """;

    private static readonly JsonSerializerOptions StringifyOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// A page to cross-check, in its own context: closed shadow roots are recorded at creation, and the clock is paused before any
    /// document loads. The engine and both oracles read the live page one after another, so a timer that replaced an element between
    /// those reads would compare them on different documents; with the clock stopped, a page reads as its load and its network left
    /// it, the same for every reader (upstream's <c>openCheckPage</c>).
    /// </summary>
    public static async Task<IPage> OpenCheckPageAsync(IBrowser browser)
    {
        var context = await browser.NewContextAsync();
        await context.AddInitScriptAsync(PageScript.RecordClosedShadowRoots);
        await context.AddInitScriptAsync(PageScript.ModalOrderInitScript);
        var start = DateTime.UtcNow;
        await context.Clock.InstallAsync(new ClockInstallOptions { TimeDate = start });
        await context.Clock.PauseAtAsync(start);
        var page = await context.NewPageAsync();
        // The init scripts run on a navigation; setContent alone is none (.NET-only: Playwright for .NET attaches them to the context the same way).
        await page.GotoAsync("about:blank");
        return page;
    }

    /// <summary>One capture through the engine's own reader, with the facts of the element behind each node id (upstream's <c>captureTagged</c>).</summary>
    public static async Task<TaggedCapture> CaptureTaggedAsync(IPage page)
    {
        var observed = await page.EvaluateAsync<JsonElement>(
            $$"""
            (arg) => {
              const result = ({{PageScript.Collect}})(arg);
              delete result.elements;
              delete result.ids;
              return result;
            }
            """,
            new
            {
                seed = 1,
                max = 5000,
                testIdAttribute = "data-testid",
                publish = true,
            }
        );
        var factsJson = await page.EvaluateAsync<JsonElement>(TagScript, new[] { Marker });
        var nodes = new List<CheckNode>();
        Flatten(observed.GetProperty("roots"), nodes);
        var facts = new Dictionary<string, ElementFacts>(StringComparer.Ordinal);
        foreach (var fact in factsJson.EnumerateObject())
        {
            // Playwright for .NET tags the objects it returns with a reference id.
            if (fact.Name == "$id")
            {
                continue;
            }

            facts[fact.Name] = new ElementFacts(
                fact.Value.GetProperty("inChildFrame").GetBoolean(),
                fact.Value.GetProperty("inClosedShadow").GetBoolean(),
                fact.Value.GetProperty("editingHost").GetBoolean(),
                fact.Value.GetProperty("svgRoot").GetBoolean()
            );
        }

        return new TaggedCapture(nodes, facts);
    }

    /// <summary>Cross-checks the page as it is now, on a page from <see cref="OpenCheckPageAsync"/>; the page keeps the marker attributes afterwards (upstream's <c>crossCheck</c>).</summary>
    public static async Task<CrossCheckResult> CrossCheckAsync(IPage page)
    {
        var (nodes, facts) = await CaptureTaggedAsync(page);
        var chrome = await ChromeTreeAsync(page);
        var disagreements = new List<Disagreement>();
        var compared = 0;
        foreach (var node in nodes)
        {
            if (
                node.Role is null
                || StructuralRoles.Contains(node.Role)
                || !facts.TryGetValue(node.Id, out var fact)
                || fact.InChildFrame
            )
            {
                continue;
            }

            if (IsUnnamedSvg(node, fact))
            {
                continue;
            }

            // The port's reader lists an aria-hidden node with its flag and every consumer of the tree skips it; upstream's walk leaves it
            // out. It is out of the tree being compared here too, as the agent reads it.
            if (node.AriaHidden)
            {
                continue;
            }

            compared += 1;
            if (!chrome.ById.TryGetValue(node.Id, out var ax))
            {
                disagreements.Add(
                    new Disagreement("chrome", "unknown", node.Role, "no AX node", Describe(node.Role, node.Name ?? ""))
                );
            }
            else
            {
                CompareWithChrome(node, ax, fact, disagreements);
            }

            var playwrightReads =
                !fact.InClosedShadow
                && !EngineNamedRoles.Contains(node.Role)
                && !NonAriaRoles.Contains(node.Role)
                && !(node.Role == "textbox" && fact.EditingHost);
            if (!string.IsNullOrEmpty(node.Name) && playwrightReads)
            {
                await CompareWithPlaywrightAsync(page, node, disagreements);
            }
        }

        foreach (var ax in chrome.Untagged)
        {
            var role = ax.Role;
            if (ax.Ignored || !InteractiveChromeRoles.Contains(role))
            {
                continue;
            }

            disagreements.Add(
                new Disagreement("chrome", "missing", "no node", role, Describe(role, Normalize(ax.Name)))
            );
        }

        return new CrossCheckResult(compared, disagreements);
    }

    /// <summary>
    /// An unnamed inline svg: Chrome's tree ignores it or leaves it out, while Playwright reads it as <c>img</c> and its aria snapshot
    /// lists it. The tree lists it as Playwright does, so an icon-only control a person sees reaches the model even when nothing names it.
    /// </summary>
    private static bool IsUnnamedSvg(CheckNode node, ElementFacts facts) =>
        facts.SvgRoot && node.Role == "image" && string.IsNullOrEmpty(node.Name);

    /// <summary>
    /// A name as <c>getByRole</c> is asked for it: exact, with icon-font glyphs allowed wherever it has a space or begins or ends
    /// (upstream's <c>glyphTolerant</c>).
    /// </summary>
    private static Regex GlyphTolerant(string name)
    {
        var words = Normalize(name).Split(' ').Select(word => EscapeRegex().Replace(word, @"\$&"));
        var gap = $@"(?:\s|{GlyphPattern})";
        return new Regex($"^{gap}*{string.Join($"{gap}+", words)}{gap}*$");
    }

    [GeneratedRegex(@"[.*+?^${}()|[\]\\/]")]
    private static partial Regex EscapeRegex();

    [GeneratedRegex(@"[\s﻿]+")]
    private static partial Regex Whitespace();

    private static string Normalize(string text) => Whitespace().Replace(text, " ").Trim();

    private static string Describe(string role, string name) =>
        $"{role} {JsonSerializer.Serialize(name, StringifyOptions)}";

    /// <summary>Chrome's names drop icon-font glyphs before they are compared (upstream's <c>replace(ICON_GLYPHS, ' ')</c>).</summary>
    private static string ReplaceIconGlyphs(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            result.Append(Rune.GetUnicodeCategory(rune) == UnicodeCategory.PrivateUse ? " " : rune.ToString());
        }

        return result.ToString();
    }

    private static void Flatten(JsonElement list, List<CheckNode> into)
    {
        foreach (var node in list.EnumerateArray())
        {
            var states = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var state in States)
            {
                states[state] = node.TryGetProperty(state, out var value) && value.ValueKind == JsonValueKind.True;
            }

            into.Add(
                new CheckNode(
                    node.GetProperty("ref").GetString()!,
                    StringOf(node, "role"),
                    StringOf(node, "name"),
                    node.TryGetProperty("level", out var level) && level.ValueKind == JsonValueKind.Number
                        ? level.GetInt32()
                        : null,
                    node.GetProperty("hidden").GetBoolean(),
                    node.GetProperty("ariaHidden").GetBoolean(),
                    states
                )
            );
            Flatten(node.GetProperty("children"), into);
        }
    }

    private static string? StringOf(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>The fields of CDP's <c>Accessibility.AXNode</c> the check reads.</summary>
    private sealed record AxNode(
        bool Ignored,
        string Role,
        string Name,
        IReadOnlyDictionary<string, JsonElement> Properties
    );

    /// <summary>Chrome's AX node for every tagged element, and the AX nodes whose element carries no tag.</summary>
    private static async Task<(Dictionary<string, AxNode> ById, List<AxNode> Untagged)> ChromeTreeAsync(IPage page)
    {
        var cdp = await page.Context.NewCDPSessionAsync(page);
        try
        {
            await cdp.SendAsync("DOM.enable");
            await cdp.SendAsync("Accessibility.enable");
            var tree = (await cdp.SendAsync("Accessibility.getFullAXTree"))!.Value;
            var byId = new Dictionary<string, AxNode>(StringComparer.Ordinal);
            var untagged = new List<AxNode>();
            foreach (var raw in tree.GetProperty("nodes").EnumerateArray())
            {
                if (!raw.TryGetProperty("backendDOMNodeId", out var backend))
                {
                    continue;
                }

                var resolved = (
                    await cdp.SendAsync(
                        "DOM.resolveNode",
                        new Dictionary<string, object> { ["backendNodeId"] = backend.GetInt32() }
                    )
                )!.Value;
                if (!resolved.GetProperty("object").TryGetProperty("objectId", out var objectId))
                {
                    continue;
                }

                var called = (
                    await cdp.SendAsync(
                        "Runtime.callFunctionOn",
                        new Dictionary<string, object>
                        {
                            ["objectId"] = objectId.GetString()!,
                            ["functionDeclaration"] = MarkerOfScript,
                            ["arguments"] = new[] { new Dictionary<string, object> { ["value"] = Marker } },
                            ["returnByValue"] = true,
                        }
                    )
                )!.Value;
                var result = called.GetProperty("result");
                var node = AxNodeOf(raw);
                if (!result.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String)
                {
                    untagged.Add(node);
                }
                else if (value.GetString() != "")
                {
                    byId[value.GetString()!] = node;
                }
            }

            return (byId, untagged);
        }
        finally
        {
            await cdp.DetachAsync();
        }
    }

    private static AxNode AxNodeOf(JsonElement raw)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (raw.TryGetProperty("properties", out var list))
        {
            foreach (var entry in list.EnumerateArray())
            {
                if (entry.GetProperty("value").TryGetProperty("value", out var value))
                {
                    properties.TryAdd(entry.GetProperty("name").GetString()!, value);
                }
            }
        }

        return new AxNode(
            raw.TryGetProperty("ignored", out var ignored) && ignored.ValueKind == JsonValueKind.True,
            ValueOf(raw, "role"),
            ValueOf(raw, "name"),
            properties
        );
    }

    // String(ax.role?.value ?? ''): the value of a CDP AXValue, as JavaScript would print it.
    private static string ValueOf(JsonElement raw, string field) =>
        raw.TryGetProperty(field, out var holder) && holder.TryGetProperty("value", out var value)
            ? value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : value.ToString()
            : "";

    /// <summary>A state as a comparable string: Chrome reports <c>true</c>, <c>false</c>, <c>mixed</c>, or leaves it out for <c>false</c> (upstream's <c>chromeState</c>).</summary>
    private static string ChromeState(AxNode node, string state) =>
        node.Properties.TryGetValue(state, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => value.ToString(),
            }
            : "false";

    private static void CompareWithChrome(CheckNode node, AxNode ax, ElementFacts facts, List<Disagreement> into)
    {
        var role = node.Role!;
        var ours = node.Name ?? "";
        var label = Describe(role, ours);
        if (EngineOnlyRoles.Contains(role) || (role == "textbox" && facts.EditingHost))
        {
            return;
        }

        if (ax.Ignored)
        {
            if (!ChromeIgnores.Contains(role))
            {
                into.Add(new Disagreement("chrome", "role", role, $"ignored ({ax.Role})", label));
            }

            return;
        }

        var accepted = ChromeRoles.TryGetValue(role, out var names) ? names : [role];
        if (!accepted.Contains(ax.Role))
        {
            into.Add(new Disagreement("chrome", "role", role, ax.Role, label));
        }

        var theirs = Normalize(ReplaceIconGlyphs(ax.Name));
        var cut = ours.Length >= 250 && theirs.StartsWith(ours[..200], StringComparison.Ordinal);
        var unnamedByChrome =
            theirs.Length == 0 && (EngineNamedRoles.Contains(role) || ChromeUnnamedRoles.Contains(role));
        if (Normalize(ours) != theirs && !cut && !unnamedByChrome)
        {
            into.Add(new Disagreement("chrome", "name", ours, theirs, label));
        }

        foreach (var state in States)
        {
            var chrome = ChromeState(ax, state);
            var engine = node.States[state] ? "true" : "false";
            // The engine has no `mixed`; a mixed control reads as not checked.
            if ((chrome == "mixed" ? "false" : chrome) != engine)
            {
                into.Add(new Disagreement("chrome", state, engine, chrome, label));
            }
        }

        if (
            role == "heading"
            && ax.Properties.TryGetValue("level", out var level)
            && level.ToString() != (node.Level?.ToString(CultureInfo.InvariantCulture) ?? "undefined")
        )
        {
            into.Add(
                new Disagreement(
                    "chrome",
                    "level",
                    node.Level?.ToString(CultureInfo.InvariantCulture) ?? "undefined",
                    level.ToString(),
                    label
                )
            );
        }
    }

    /// <summary>Whether <c>getByRole(role, { name, exact })</c> finds the engine's element among its matches (upstream's <c>compareWithPlaywright</c>).</summary>
    private static async Task CompareWithPlaywrightAsync(IPage page, CheckNode node, List<Disagreement> into)
    {
        var name = node.Name ?? "";
        var role = AriaRole.TryGetValue(node.Role!, out var spelled) ? spelled : node.Role!;
        var label = Describe(node.Role!, name);
        try
        {
            if (!Enum.TryParse<Microsoft.Playwright.AriaRole>(role, ignoreCase: true, out var ariaRole))
            {
                throw new PlaywrightException($"Unknown role: {role}");
            }

            // `includeHidden` also changes how Playwright computes names, so it is set only for a node the tree itself reports hidden.
            var found = await page.GetByRole(
                    ariaRole,
                    new PageGetByRoleOptions { NameRegex = GlyphTolerant(name), IncludeHidden = node.Hidden }
                )
                .EvaluateAllAsync<bool>(
                    "(elements, [attribute, id]) => elements.some((element) => element.getAttribute(attribute) === id)",
                    new[] { Marker, node.Id }
                );
            if (!found)
            {
                into.Add(new Disagreement("playwright", "getByRole", "match", "no match", label));
            }
        }
        catch (PlaywrightException error)
        {
            into.Add(
                new Disagreement("playwright", "getByRole", "match", $"error: {error.Message.Split('\n')[0]}", label)
            );
        }
    }
}
