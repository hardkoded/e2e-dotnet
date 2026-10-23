// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using E2E.Engine;
using E2E.Internal;

namespace E2E.Cli.Mcp;

/// <summary>What a session's test lines need of the session around them. Upstream's <c>TestLineOptions</c>.</summary>
internal sealed class TestLineOptions
{
    /// <summary>The newest observation's tree: the screen the agent's node refs name. Null when nothing was observed yet.</summary>
    public required Func<IReadOnlyList<SemanticNode>?> Newest { get; init; }

    /// <summary>One look at the live screen, which every locator tried for an action is resolved against.</summary>
    public required Func<CancellationToken, Task<Observation>> Observe { get; init; }

    /// <summary>The attempt's secret ledger, for what a node says before it goes into a line.</summary>
    public required Func<string, string> Redact { get; init; }

    /// <summary>The app's base URL, which a navigation line is made relative to.</summary>
    public Uri? AppBase { get; init; }

    /// <summary>The <c>secrets</c> names <c>Secrets.Get</c> takes.</summary>
    public IReadOnlyCollection<string> Secrets { get; init; } = [];

    /// <summary>Every node a locator matches on a look; tests count the tries through it.</summary>
    public Func<Observation, LocatorExpression, IReadOnlyList<SemanticNode>> Resolve { get; init; } = static (observation, expression) => LocatorResolver.Resolve(observation, expression);
}

/// <summary>
/// The test line each grammar action of an <c>e2e mcp</c> session answers with: the <c>Screen.*</c> or <c>App.*</c> call that
/// repeats the action in a test, so a coding agent can write the test while it walks the flow instead of calling
/// <c>locate</c> for every control afterwards. The locator comes from the target as the newest observation listed it, read
/// before the action runs, since the action may remove it. It is tried in the order the skill recommends (role and name,
/// label, placeholder, text, test id) against the live screen, as <c>locate</c> tries one: a locator that matches the target
/// and nothing else is printed, one that matches its twins too is narrowed (to the nodes that render, under a container, or
/// by position, in that order), and otherwise the result says why there is no line. The session keeps the lines in order, and
/// <c>test_lines</c> returns them as a draft test body. Upstream's <c>mcp/test-lines.ts</c>.
/// </summary>
internal sealed class SessionTestLines(TestLineOptions options)
{
    /// <summary>
    /// Locators tried per target before giving up on a line. Upstream spends one engine resolve on each, a whole snapshot on a
    /// device. The port looks once per target, so the bound only keeps a control nothing names from trying every narrowing.
    /// </summary>
    private const int MaxTries = 10;

    /// <summary>Containers tried, nearest first, to narrow a locator that matches the target's twins too.</summary>
    private const int MaxContainers = 3;

    /// <summary>CSS pixels a match's center may sit outside the target's box and still be the target.</summary>
    private const double BoxSlack = 1;

    /// <summary>The roles <c>GetByRole</c> takes, so a node read as <c>generic</c> never becomes a role query.</summary>
    private static readonly HashSet<string> Roles = new(
        [
            "button", "link", "textbox", "searchbox", "combobox", "listbox", "option", "checkbox",
            "radio", "radiogroup", "switch", "slider", "spinbutton", "progressbar", "meter", "image",
            "heading", "tab", "tablist", "tabpanel", "menu", "menubar", "menuitem", "menuitemcheckbox",
            "menuitemradio", "toolbar", "tooltip", "tree", "treeitem", "list", "listitem", "table",
            "grid", "row", "rowgroup", "rowheader", "cell", "gridcell", "columnheader", "separator",
            "group", "article", "figure", "form", "status", "alert", "dialog", "alertdialog", "main",
            "navigation", "banner", "contentinfo", "complementary", "region",
        ],
        StringComparer.Ordinal);

    /// <summary>The grammar tools that print a line or say why they print none.</summary>
    private static readonly HashSet<string> LineTools = new(
        ["tap", "double_tap", "type", "type_secret", "press", "select", "check", "uncheck", "clear", "scroll", "scroll_to", "navigate", "back"],
        StringComparer.Ordinal);

    /// <summary>The locator method each node verb's tool repeats as.</summary>
    private static readonly Dictionary<string, string> NodeMethods = new(StringComparer.Ordinal)
    {
        ["tap"] = "TapAsync",
        ["double_tap"] = "DoubleTapAsync",
        ["check"] = "CheckAsync",
        ["uncheck"] = "UncheckAsync",
        ["clear"] = "ClearAsync",
    };

    private readonly TestLineOptions _options = options;
    private readonly List<Entry> _entries = [];

    /// <summary>The tools whose results carry a line, so a new grammar verb needs a line or a reason.</summary>
    public static bool HasLine(string tool) => LineTools.Contains(tool);

    /// <summary>Records the session's opening of the app, which a test does with <c>App.OpenAsync()</c>.</summary>
    public void Opened() => _entries.Add(new Entry("open_session", "await App.OpenAsync();", null));

    /// <summary>A grammar tool that works out its line before the action and adds it to the result after.</summary>
    public CatalogTool Wrap(string name, CatalogTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (!LineTools.Contains(name) || tool.Execute is not { } execute)
        {
            return tool;
        }

        return new CatalogTool
        {
            Description = tool.Description,
            InputSchema = tool.InputSchema,
            ToModelOutput = tool.ToModelOutput,
            Execute = async (args, token) =>
            {
                var plan = await PlanAsync(name, args, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                var output = await execute(args, token).ConfigureAwait(false);
                return output is string text ? Annotate(name, text, plan) : output;
            },
        };
    }

    /// <summary>The read-only <c>test_lines</c> catalog tool.</summary>
    public CatalogTool Tool()
    {
        return new CatalogTool
        {
            Description = "Every test line this session's actions printed so far, in order, as a draft test body: the Screen.* and App.* calls that repeat each action, with a comment where an action has none. Paste it into a test, then add the assertions.",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { }, additionalProperties = false }),
            Execute = (_, _) => Task.FromResult<object?>(Draft()),
        };
    }

    /// <summary>The recorded lines as a draft test body.</summary>
    public string Draft()
    {
        if (_entries.Count == 0)
        {
            return "No test lines yet. Each action (tap, type, select, check, and the rest) prints the line that repeats it in a test, and this tool returns them in order.";
        }

        var body = _entries.Select(entry => entry.Line ?? "// " + entry.Tool + ": no test line: " + entry.Reason);
        return string.Join(
            '\n',
            [
                "Draft test body from this session's actions, in order (" + _entries.Count(entry => entry.Line is not null).ToString(CultureInfo.InvariantCulture) + " lines):",
                "",
                .. body,
                "",
                "Paste it into a test method, and add an Expect after each step that should change the screen.",
            ]);
    }

    /// <summary>
    /// The queries a node's own fields give, in the order the skill recommends: role and name, then label, placeholder, text,
    /// and test id. Upstream's <c>nodeQueries</c>; its tree carries no test id, so it reads the id off the live node, where the
    /// port's tree has it.
    /// </summary>
    internal static IReadOnlyList<LocateArgs> NodeQueries(SemanticNode node, Func<string, string> redact)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(redact);
        var queries = new List<LocateArgs>();
        var name = Clean(node.Name, redact);
        if (name is not null && node.Role is { } role && Roles.Contains(role))
        {
            queries.Add(new LocateArgs { Role = role, Name = name });
        }

        if (name is not null)
        {
            queries.Add(new LocateArgs { Label = name });
        }

        if (Clean(node.Placeholder, redact) is { } placeholder)
        {
            queries.Add(new LocateArgs { Placeholder = placeholder });
        }

        foreach (var text in new[] { Clean(node.Text, redact), name }.OfType<string>().Distinct(StringComparer.Ordinal))
        {
            queries.Add(new LocateArgs { Text = text });
        }

        if (Clean(node.TestId, redact) is { } testId)
        {
            queries.Add(new LocateArgs { TestId = testId });
        }

        return queries;
    }

    /// <summary>
    /// The <c>App.OpenAsync</c> line for a navigation: a URL on the app's origin as its path, so the line holds on the port the
    /// next run gets, the base URL itself as <c>App.OpenAsync()</c>, anything else as given.
    /// </summary>
    internal static string NavigationLine(string url, Uri? appBase)
    {
        if (appBase is not null && Uri.TryCreate(appBase, url, out var resolved))
        {
            if (resolved.AbsoluteUri == appBase.AbsoluteUri)
            {
                return "await App.OpenAsync();";
            }

            if (resolved.GetLeftPart(UriPartial.Authority) == appBase.GetLeftPart(UriPartial.Authority))
            {
                return "await App.OpenAsync(" + SessionCatalog.Quote(resolved.PathAndQuery + resolved.Fragment) + ");";
            }
        }

        return "await App.OpenAsync(" + SessionCatalog.Quote(url) + ");";
    }

    /// <summary>Records a performed action's line and puts it after the result's lead.</summary>
    private string Annotate(string tool, string text, LinePlan plan)
    {
        _entries.Add(new Entry(tool, plan.Line, plan.Reason));
        var note = plan.Line is { } line ? "Test line: " + line : "No test line: " + plan.Reason + ".";
        var cut = text.IndexOf('\n', StringComparison.Ordinal);
        return cut < 0 ? text + "\n" + note : text[..cut] + "\n" + note + text[cut..];
    }

    /// <summary>The line an action will print, worked out before it runs, while its target is still on screen.</summary>
    private async Task<LinePlan> PlanAsync(string name, JsonElement args, CancellationToken token)
    {
        try
        {
            return await BuildAsync(name, args, token).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A line that cannot be worked out is a reason the action still runs with, never a failure of the action.
        catch (Exception cause) when (cause is not OperationCanceledException)
#pragma warning restore CA1031
        {
            return LinePlan.None("trying its locator failed: " + TextRules.Normalize(Tools.CodedMessage(cause)));
        }
    }

    private async Task<LinePlan> BuildAsync(string name, JsonElement args, CancellationToken token)
    {
        var target = Field(args, "target");
        if (NodeMethods.TryGetValue(name, out var method))
        {
            if (name is "tap" or "double_tap" && args.TryGetProperty("modifiers", out var held) && held.ValueKind == JsonValueKind.Array && held.GetArrayLength() > 0)
            {
                return LinePlan.None("a locator " + (name == "tap" ? "tap" : "double tap") + " takes no modifier keys");
            }

            return await OnTargetAsync(target, locator => locator + "." + method + "()", token).ConfigureAwait(false);
        }

        switch (name)
        {
            case "type":
                {
                    // A value the ledger rewrites is a secret: the line would fill the placeholder, not the secret.
                    var text = Field(args, "text") ?? "";
                    return _options.Redact(text) != text
                        ? LinePlan.None("the text holds a secret value; fill it with type_secret for a line")
                        : await OnTargetAsync(target, locator => locator + ".FillAsync(" + SessionCatalog.Quote(text) + ")", token).ConfigureAwait(false);
                }

            case "type_secret":
                {
                    var secret = Field(args, "secret") ?? "";
                    return _options.Secrets.Contains(secret)
                        ? await OnTargetAsync(target, locator => locator + ".FillAsync(Secrets.Get(" + SessionCatalog.Quote(secret) + "))", token).ConfigureAwait(false)
                        : LinePlan.None("the secret is not a config secret");
                }

            case "press":
                return target is null
                    ? LinePlan.None("the key went to whatever had focus; press it on a listed node by ref for a line")
                    : await OnTargetAsync(target, locator => locator + ".PressAsync(" + SessionCatalog.Quote(Field(args, "key") ?? "Enter") + ")", token).ConfigureAwait(false);
            case "select":
                return await OnTargetAsync(target, locator => locator + ".SelectOptionAsync(" + SessionCatalog.Quote(Field(args, "value") ?? "") + ")", token).ConfigureAwait(false);
            case "scroll_to":
                return target is not null && Field(args, "text") is null
                    ? await OnTargetAsync(target, locator => locator + ".ScrollIntoViewAsync()", token).ConfigureAwait(false)
                    : LinePlan.None("scrolling to a text has none; a test action brings its node into view");
            case "scroll":
                return LinePlan.None("a test action brings its node into view itself");
            case "navigate":
                return LinePlan.Of(NavigationLine(Field(args, "url") ?? "", _options.AppBase));
            case "back":
                return LinePlan.Of("await App.BackAsync();");
            default:
                return LinePlan.None(name + " has no test line");
        }
    }

    /// <summary>A line acting on one target, or why there is none.</summary>
    private async Task<LinePlan> OnTargetAsync(string? reference, Func<string, string> act, CancellationToken token)
    {
        var found = await LocatorAsync(reference, token).ConfigureAwait(false);
        return found.Reason is { } reason
            ? LinePlan.None(reason)
            : LinePlan.Of("await " + act(found.Code!) + ";" + (found.Comment is null ? "" : " // " + found.Comment));
    }

    /// <summary>
    /// The locator a test line uses for a target ref: the first query from the node's own fields that matches it alone; else the
    /// query that matched it among its twins, kept to the nodes that render or scoped under a container that tells them apart;
    /// else that query by position, with a comment saying so, when its place among the twins is certain.
    /// </summary>
    private async Task<Found> LocatorAsync(string? reference, CancellationToken token)
    {
        if (reference is null)
        {
            return Found.None("the action names no node");
        }

        var listed = Listed(reference);
        if (listed is null)
        {
            return Found.None("node " + reference + " is not on the newest screen");
        }

        var live = await _options.Observe(token).ConfigureAwait(false);
        var tries = MaxTries;
        Probe? Attempt(LocatorExpression expression, IReadOnlyList<SemanticNode>? within = null)
        {
            if (tries <= 0)
            {
                return null;
            }

            tries--;
            return ProbeOf(live, expression, listed, within ?? listed.Roots);
        }

        (Located Located, Probe Probe)? twins = null;
        var queries = NodeQueries(listed.Node, _options.Redact);
        foreach (var query in queries)
        {
            var located = Located.Of(SessionCatalog.LocateQuery(query));
            var probe = Attempt(located.Expression);
            if (probe is null)
            {
                break;
            }

            if (probe.Count == 1 && probe.Index == 0)
            {
                return Found.Of(located);
            }

            if (probe.Count > 1 && probe.Index >= 0 && (twins is null || (probe.Certain && !twins.Value.Probe.Certain)))
            {
                twins = (located, probe);
            }
        }

        if (twins is not { } best)
        {
            if (tries <= 0)
            {
                return Found.None("no locator found it alone within " + MaxTries.ToString(CultureInfo.InvariantCulture) + " tries; try locate");
            }

            return Found.None(
                queries.Count == 0
                    ? "the node has no name, text, placeholder, or test id to find it by"
                    : "no locator from its role and name, label, placeholder, text, or test id finds it on the live screen; try locate");
        }

        if (VisibleOnly(best.Located) is { } visible && Attempt(visible.Expression) is { Count: 1, Index: 0 })
        {
            return Found.Of(visible);
        }

        foreach (var scope in ContainerScopes(listed))
        {
            if (best.Located.Expression is not { Kind: not ("filter" or "index"), Parent: null } child)
            {
                break;
            }

            var probe = Attempt(child with { Parent = scope.Located.Expression }, scope.Within);
            if (probe is null)
            {
                break;
            }

            if (probe.Count == 1 && probe.Index == 0)
            {
                return Found.Of(new Located(child with { Parent = scope.Located.Expression }, scope.Located.Code + best.Located.Code["Screen".Length..]));
            }
        }

        var (count, index, certain) = (best.Probe.Count, best.Probe.Index, best.Probe.Certain);

        // A position is only as good as the evidence for it: a box from the observation may have moved onto a twin since.
        if (!certain)
        {
            return Found.None("it shares " + best.Located.Code + " with " + (count - 1).ToString(CultureInfo.InvariantCulture) + " other nodes, and nothing on the screen tells it apart for certain");
        }

        return new Found(
            new Located(new LocatorExpression { Kind = "index", Source = best.Located.Expression, Index = index }, best.Located.Code + (index == 0 ? ".First()" : ".Nth(" + index.ToString(CultureInfo.InvariantCulture) + ")")),
            (index + 1).ToString(CultureInfo.InvariantCulture) + " of " + count.ToString(CultureInfo.InvariantCulture) + " matches, by position; a unique name or test id holds up better",
            null);
    }

    /// <summary>
    /// How many nodes a locator matches now, and which of them is the target. When the matches that read like the target are the
    /// twins the observation listed under <paramref name="within"/>, each where it was or all moved together, the target's place
    /// is certain: the twin in its turn, or, when the engine lists them in another order than the observation (light DOM before
    /// shadow roots), the one still in its box. Otherwise it is the one match centered in its box from the observation, as an
    /// inner text element sits inside the button it names, and the place is not certain.
    /// </summary>
    private Probe ProbeOf(Observation live, LocatorExpression expression, ListedNode listed, IReadOnlyList<SemanticNode> within)
    {
        var matches = _options.Resolve(live, expression);
        var count = matches.Count;
        var target = listed.Node;
        var self = Identity(target, _options.Redact);
        var alike = Enumerable.Range(0, count).Where(index => Identity(matches[index], _options.Redact) == self).ToList();
        var twins = TwinsOf(within, target, _options.Redact);
        if (alike.Count == twins.Count)
        {
            var turn = twins.FindIndex(twin => ReferenceEquals(twin, target));
            var likes = alike.Select(index => matches[index]).ToList();
            if (twins.Count == 1 || MovedTogether(twins, likes))
            {
                return new Probe(count, alike[turn], true);
            }

            var still = alike.Where(index => CenteredIn(matches[index], target)).ToList();
            if (still.Count == 1 && StayedPut(twins, likes))
            {
                return new Probe(count, still[0], true);
            }
        }

        var boxed = Enumerable.Range(0, count).Where(index => CenteredIn(matches[index], target)).ToList();
        return new Probe(count, boxed.Count == 1 ? boxed[0] : -1, false);
    }

    /// <summary>The node a ref names on the newest screen, with its ancestors, nearest first, and the screen's root.</summary>
    private ListedNode? Listed(string reference)
    {
        if (_options.Newest() is not { Count: > 0 } roots)
        {
            return null;
        }

        var parents = new Dictionary<SemanticNode, SemanticNode?>(ReferenceEqualityComparer.Instance);
        SemanticNode? found = null;
        void Walk(SemanticNode node, SemanticNode? parent)
        {
            parents[node] = parent;
            if (string.Equals(node.Ref, reference, StringComparison.Ordinal))
            {
                found = node;
            }

            foreach (var child in node.Children)
            {
                Walk(child, node);
            }
        }

        foreach (var root in roots)
        {
            Walk(root, null);
        }

        if (found is null)
        {
            return null;
        }

        var ancestors = new List<SemanticNode>();
        for (var parent = parents[found]; parent is not null; parent = parents[parent])
        {
            ancestors.Add(parent);
        }

        return new ListedNode(found, ancestors, roots);
    }

    /// <summary>
    /// The containers a locator can be scoped under to tell the target from its twins, nearest first: an ancestor with a role, by
    /// its name, or by the first text it holds outside the target, the way a test scopes the "Delete" button of one row.
    /// </summary>
    private List<(Located Located, IReadOnlyList<SemanticNode> Within)> ContainerScopes(ListedNode listed)
    {
        var scopes = new List<(Located Located, IReadOnlyList<SemanticNode> Within)>();
        var containers = 0;
        foreach (var ancestor in listed.Ancestors)
        {
            if (ancestor.Role is not { } role || !Roles.Contains(role))
            {
                continue;
            }

            if (containers >= MaxContainers)
            {
                break;
            }

            containers++;
            var name = Clean(ancestor.Name, _options.Redact);
            if (name is not null)
            {
                scopes.Add((Located.Of(SessionCatalog.LocateQuery(new LocateArgs { Role = role, Name = name })), [ancestor]));
            }

            var text = FirstText(ancestor, listed.Node) ?? Clean(ancestor.Text, _options.Redact);
            if (text is not null && text != name)
            {
                var root = SessionCatalog.LocateQuery(new LocateArgs { Role = role });
                TextMatch? hasText = text;
                scopes.Add((new Located(new LocatorExpression { Kind = "filter", Source = root.Query, HasText = hasText }, root.Code + ".Filter(" + SessionCatalog.Quote(text) + ")"), [ancestor]));
            }
        }

        return scopes;
    }

    /// <summary>The first text a container holds, depth first, outside the subtree of <paramref name="skip"/>.</summary>
    private string? FirstText(SemanticNode node, SemanticNode skip)
    {
        foreach (var child in node.Children)
        {
            if (ReferenceEquals(child, skip))
            {
                continue;
            }

            if (child.Children.Count == 0)
            {
                if ((Clean(child.Text, _options.Redact) ?? Clean(child.Name, _options.Redact)) is { } own)
                {
                    return own;
                }

                continue;
            }

            if (FirstText(child, skip) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    /// <summary>
    /// A bare query kept to the nodes that render, the way a test steps past a hidden twin (<c>Visible = true</c>); null for
    /// anything else.
    /// </summary>
    private static Located? VisibleOnly(Located located)
    {
        var (expression, code) = (located.Expression, located.Code);
        if (expression is not { Kind: not ("filter" or "index"), Parent: null } || code.Contains('{', StringComparison.Ordinal))
        {
            return null;
        }

        var options = expression.Kind == "role" ? "RoleOptions" : "TextMatchOptions";
        return new Located(expression with { Visible = true }, code[..^1] + ", new " + options + " { Visible = true })");
    }

    /// <summary>Whether a live node sits with its center inside the target's box from the observation.</summary>
    private static bool CenteredIn(SemanticNode match, SemanticNode target)
    {
        if (target.Rect is not { } box || match.Rect is not { } found)
        {
            return false;
        }

        var x = found.X + (found.Width / 2);
        var y = found.Y + (found.Height / 2);
        return x >= box.X - BoxSlack && x <= box.X + box.Width + BoxSlack && y >= box.Y - BoxSlack && y <= box.Y + box.Height + BoxSlack;
    }

    /// <summary>What a node reads as: its role, name, text, and placeholder, in the one-line form, through the ledger.</summary>
    private static string Identity(SemanticNode node, Func<string, string> redact)
    {
        string Field(string? value) => Clean(value, redact) ?? "";
        return string.Join('\0', node.Role ?? "", Field(node.Name), Field(node.Text), Field(node.Placeholder));
    }

    /// <summary>The nodes under <paramref name="roots"/> that read like the target, in the observation's order, the target among them.</summary>
    private static List<SemanticNode> TwinsOf(IEnumerable<SemanticNode> roots, SemanticNode target, Func<string, string> redact)
    {
        var self = Identity(target, redact);
        var twins = new List<SemanticNode>();
        void Walk(SemanticNode node)
        {
            if (Identity(node, redact) == self)
            {
                twins.Add(node);
            }

            foreach (var child in node.Children)
            {
                Walk(child);
            }
        }

        foreach (var root in roots)
        {
            Walk(root);
        }

        return twins;
    }

    /// <summary>Whether each live twin, taken in turn, sits where its observed twin sat, every one moved by the same offset.</summary>
    private static bool MovedTogether(IReadOnlyList<SemanticNode> observed, IReadOnlyList<SemanticNode> live)
    {
        (double X, double Y)? shift = null;
        for (var index = 0; index < observed.Count; index++)
        {
            if (observed[index].Rect is not { } was || live[index].Rect is not { } now)
            {
                return false;
            }

            var moved = (X: now.X - was.X, Y: now.Y - was.Y);
            shift ??= moved;
            if (Math.Abs(moved.X - shift.Value.X) > BoxSlack || Math.Abs(moved.Y - shift.Value.Y) > BoxSlack)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether every live twin is centered in the box of exactly one observed twin, each box taken once: the same twins, none moved.</summary>
    private static bool StayedPut(IReadOnlyList<SemanticNode> observed, IReadOnlyList<SemanticNode> live)
    {
        var taken = new HashSet<SemanticNode>(ReferenceEqualityComparer.Instance);
        foreach (var match in live)
        {
            var boxes = observed.Where(twin => CenteredIn(match, twin)).ToList();
            if (boxes.Count != 1 || !taken.Add(boxes[0]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Text in the one-line form a locator matches against, or null for none.</summary>
    internal static string? Clean(string? text, Func<string, string> redact)
    {
        if (text is null)
        {
            return null;
        }

        var collapsed = TextRules.Normalize(redact(text));
        return collapsed.Length == 0 ? null : collapsed;
    }

    private static string? Field(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>One recorded action: its line, or why it has none.</summary>
    private sealed record Entry(string Tool, string? Line, string? Reason);

    /// <summary>What an action prints: the line, or why there is none.</summary>
    private sealed record LinePlan(string? Line, string? Reason)
    {
        public static LinePlan Of(string line) => new(line, null);

        public static LinePlan None(string reason) => new(null, reason);
    }

    /// <summary>A locator: its expression for the resolver and its code for the test.</summary>
    private sealed record Located(LocatorExpression Expression, string Code)
    {
        public static Located Of(LocateQuery query) => new(query.Query, query.Code);
    }

    /// <summary>What a locator search found: the locator and how the line comments on it, or why there is none.</summary>
    private sealed record Found(Located? Located, string? Comment, string? Reason)
    {
        public string? Code => Located?.Code;

        public static Found Of(Located located) => new(located, null, null);

        public static Found None(string reason) => new(null, null, reason);
    }

    /// <summary>A node the newest observation listed, with its ancestors, nearest first, and the screen's roots.</summary>
    private sealed record ListedNode(SemanticNode Node, IReadOnlyList<SemanticNode> Ancestors, IReadOnlyList<SemanticNode> Roots);

    /// <summary>What one locator matched: how many nodes, which of them is the target (-1 when none is placed as it), and whether that place is certain.</summary>
    private sealed record Probe(int Count, int Index, bool Certain);
}
