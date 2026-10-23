// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using E2E.Engine;
using E2E.Internal;

namespace E2E.Cli.Mcp;

/// <summary>The app's base URL as a line is made relative to it: upstream's <c>{ href, origin }</c>.</summary>
internal sealed record AppBase(string Href, string Origin);

/// <summary>The names a test fills values from, as the config declares them.</summary>
internal sealed class TestLineFills
{
    /// <summary>Each credential: the name <c>Credentials.User</c> takes, its username, and its password secret's name.</summary>
    public IReadOnlyList<(string Name, string Username, string Password)> Credentials { get; init; } = [];

    /// <summary>The <c>secrets</c> names <c>Secrets.Get</c> takes.</summary>
    public IReadOnlyList<string> Secrets { get; init; } = [];
}

/// <summary>What a line needs of the session around it.</summary>
internal sealed class TestLineOptions
{
    /// <summary>Builds a query's expression and <c>Screen.*</c> code, as <c>locate</c> does (<see cref="SessionCatalog.LocateQuery"/>).</summary>
    public required Func<LocateArgs, LocateQuery> Query { get; init; }

    /// <summary>The newest observation the session's tools took: the screen a node id is looked up on.</summary>
    public required Func<Observation?> Newest { get; init; }

    /// <summary>Every node an expression matches on the live screen now, without waiting.</summary>
    public required Func<LocatorExpression, CancellationToken, Task<IReadOnlyList<SemanticNode>>> Resolve { get; init; }

    /// <summary>The attempt's secret ledger, for what a node says before it goes into a line.</summary>
    public required Func<string, string> Redact { get; init; }

    /// <summary>The app's base URL, which a navigation line is made relative to.</summary>
    public AppBase? AppBase { get; init; }

    /// <summary>The configured credentials and secrets a fill is written with.</summary>
    public TestLineFills? Fills { get; init; }
}

/// <summary>
/// The test line each grammar action of an <c>e2e mcp</c> session answers with: the <c>Screen.*</c> or <c>App.*</c> call that repeats the
/// action in a test, so a coding agent can write the test while it walks the flow instead of calling <c>locate</c> for every control
/// afterwards. The locator comes from the target as the newest observation listed it, read before the action runs, since the action may
/// remove it. It is tried in the order the skill recommends (role and name, label, placeholder, text, test id) against the live screen, as
/// <c>locate</c> tries one: a locator that matches the target and nothing else is printed, one that matches its twins too is narrowed (to
/// the nodes that render, under a container, or by position, in that order), and otherwise the result says why there is no line. The
/// session keeps the lines in order, and <c>test_lines</c> returns them as a draft test body. Only the session catalog wraps its grammar
/// this way; the testing agent's model loop never sees a line. Upstream's <c>SessionTestLines</c> (<c>mcp/test-lines.ts</c>).
/// </summary>
internal sealed class SessionTestLines(TestLineOptions options)
{
    /// <summary>Matches read per locator while looking for the target among them.</summary>
    private const int MaxReadMatches = 50;

    /// <summary>
    /// Locators tried per target before giving up on a line. Each is one engine resolve, which on a device is a whole snapshot, so a
    /// control nothing names costs a bounded wait rather than every narrowing there is.
    /// </summary>
    private const int MaxTries = 10;

    /// <summary>Containers tried, nearest first, to narrow a locator that matches the target's twins too.</summary>
    private const int MaxContainers = 3;

    /// <summary>CSS pixels a match's center may sit outside the target's box and still be the target.</summary>
    private const double BoxSlack = 1;

    /// <summary>The roles <c>GetByRole</c> takes, so a node read as <c>generic</c> never becomes a role query.</summary>
    private static readonly HashSet<string> Roles = new(
        [
            "button", "link", "textbox", "searchbox", "combobox", "listbox", "option", "checkbox", "radio", "radiogroup", "switch", "slider",
            "spinbutton", "progressbar", "meter", "image", "heading", "tab", "tablist", "tabpanel", "menu", "menubar", "menuitem", "menuitemcheckbox",
            "menuitemradio", "toolbar", "tooltip", "tree", "treeitem", "list", "listitem", "table", "grid", "row", "rowgroup", "rowheader", "cell",
            "gridcell", "columnheader", "separator", "group", "article", "figure", "form", "status", "alert", "dialog", "alertdialog", "main",
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
    };

    private readonly List<Entry> _entries = [];
    private Screen? _screen;

    /// <summary>Records the session's opening of the app, which a test does with <c>App.OpenAsync()</c>.</summary>
    public void Opened() => _entries.Add(new Entry("open_session", "await App.OpenAsync();", null));

    /// <summary>A grammar tool that has a line answers with it after its lead; any other tool is returned as it is.</summary>
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
                // Before the action: it may remove its target from the screen.
                var plan = await PlanAsync(name, args, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                // A failed action throws here: it prints and records no line.
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

        var body = _entries.Select(entry => entry.Line ?? "// " + entry.Tool + ": no test line: " + entry.Reason).ToList();
        return string.Join(
            '\n',
            [
                "Draft test body from this session's actions, in order (" + _entries.Count(entry => entry.Line is not null).ToString(CultureInfo.InvariantCulture) + " lines):",
                string.Empty,
                .. body,
                string.Empty,
                "Paste it into a test method, and add an Expect after each step that should change the screen.",
            ]);
    }

    /// <summary>The words a result gives for an action that has no line, after "No test line:".</summary>
    private string Annotate(string name, string text, LinePlan plan)
    {
        var entry = new Entry(name, plan.Line, plan.Reason);
        _entries.Add(entry);
        var note = entry.Line is not null ? "Test line: " + entry.Line : "No test line: " + entry.Reason + ".";
        var cut = text.IndexOf('\n', StringComparison.Ordinal);
        return cut == -1 ? text + "\n" + note : text[..cut] + "\n" + note + text[cut..];
    }

    /// <summary>The line an action will print, worked out before it runs, while its target is still on screen.</summary>
    private async Task<LinePlan> PlanAsync(string name, JsonElement input, CancellationToken token)
    {
        // The newest observation is the screen a node id names.
        _screen = options.Newest() is { } newest ? IndexScreen(newest) : null;
        try
        {
            return await PlanCoreAsync(name, input, token).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A locator the engine could not try is a reason the action has no line; a cancel is not.
        catch (Exception cause) when (cause is not OperationCanceledException)
#pragma warning restore CA1031
        {
            return LinePlan.None("trying its locator failed: " + Tools.CodedMessage(cause));
        }
    }

    private async Task<LinePlan> PlanCoreAsync(string name, JsonElement input, CancellationToken token)
    {
        var target = StringField(input, "target");
        if (NodeMethods.TryGetValue(name, out var method))
        {
            if (input.ValueKind == JsonValueKind.Object && input.TryGetProperty("modifiers", out var held) && held.ValueKind == JsonValueKind.Array && held.GetArrayLength() > 0)
            {
                return LinePlan.None("a test locator takes no held keys, so no line repeats a " + name.Replace('_', ' ') + " with modifiers");
            }

            return await OnTargetAsync(target, locator => locator + "." + method + "()", token).ConfigureAwait(false);
        }

        switch (name)
        {
            case "type":
                {
                    if (target is null)
                    {
                        return LinePlan.None("the value went to the focused field; type into a listed node by id for a line");
                    }

                    var value = StringField(input, "text") ?? string.Empty;
                    return await OnTargetAsync(target, locator => locator + ".FillAsync(" + Typed(value) + ")", token).ConfigureAwait(false);
                }

            case "type_secret":
                {
                    var secret = SecretExpression(StringField(input, "secret") ?? string.Empty);
                    return secret is null
                        ? LinePlan.None("the secret is neither a credential password nor a config secret")
                        : await OnTargetAsync(target, locator => locator + ".FillAsync(" + secret + ")", token).ConfigureAwait(false);
                }

            case "press":
                return target is null
                    ? LinePlan.None("the key went to whatever had focus; press it on a listed node by id for a line")
                    : await OnTargetAsync(target, locator => locator + ".PressAsync(" + SessionCatalog.Quote(StringField(input, "key") ?? string.Empty) + ")", token).ConfigureAwait(false);
            case "select":
                return await OnTargetAsync(target, locator => locator + ".SelectOptionAsync(" + SessionCatalog.Quote(StringField(input, "value") ?? string.Empty) + ")", token).ConfigureAwait(false);
            case "check":
                return await OnTargetAsync(target, locator => locator + ".CheckAsync()", token).ConfigureAwait(false);
            case "uncheck":
                return await OnTargetAsync(target, locator => locator + ".UncheckAsync()", token).ConfigureAwait(false);
            case "clear":
                return await OnTargetAsync(target, locator => locator + ".ClearAsync()", token).ConfigureAwait(false);
            case "scroll_to":
                return target is not null && StringField(input, "text") is null
                    ? await OnTargetAsync(target, locator => locator + ".ScrollIntoViewAsync()", token).ConfigureAwait(false)
                    : LinePlan.None("scrolling to a text has none; a test action brings its node into view itself");
            case "scroll":
                return LinePlan.None("a test action brings its node into view itself");
            case "navigate":
                return LinePlan.Of(NavigationLine(StringField(input, "url") ?? string.Empty, options.AppBase));
            case "back":
                return LinePlan.Of("await App.BackAsync();");
            default:
                return LinePlan.None(name + " has no test line");
        }
    }

    /// <summary>A line acting on one target, or why there is none.</summary>
    private async Task<LinePlan> OnTargetAsync(string? id, Func<string, string> act, CancellationToken token)
    {
        var found = await LocatorAsync(id, token).ConfigureAwait(false);
        return found.Reason is not null
            ? LinePlan.None(found.Reason)
            : LinePlan.Of("await " + act(found.Code!) + ";" + (found.Comment is null ? string.Empty : " // " + found.Comment));
    }

    /// <summary>A typed value as the line spells it: a credential's username by its name, anything else as written.</summary>
    private string Typed(string value)
    {
        var credentials = options.Fills?.Credentials ?? [];
        foreach (var credential in credentials)
        {
            if (string.Equals(credential.Username, value, StringComparison.Ordinal))
            {
                return "Credentials.User(" + SessionCatalog.Quote(credential.Name) + ").Username";
            }
        }

        return SessionCatalog.Quote(value);
    }

    /// <summary>How a test names a secret <c>type_secret</c> filled: a credential's password, or a config secret.</summary>
    private string? SecretExpression(string name)
    {
        foreach (var credential in options.Fills?.Credentials ?? [])
        {
            if (string.Equals(credential.Password, name, StringComparison.Ordinal))
            {
                return "Credentials.User(" + SessionCatalog.Quote(credential.Name) + ").Password";
            }
        }

        return options.Fills?.Secrets.Contains(name) == true ? "Secrets.Get(" + SessionCatalog.Quote(name) + ")" : null;
    }

    /// <summary>
    /// The locator a test line uses for a target id: the first query from the node's own fields that matches it alone; else the query
    /// that matched it among its twins, kept to the nodes that render or scoped under a container that tells them apart; else that query
    /// by position, with a comment saying so, when its place among the twins is certain.
    /// </summary>
    private async Task<Found> LocatorAsync(string? id, CancellationToken token)
    {
        if (id is null)
        {
            return Found.None("the action names no node");
        }

        var listed = Listed(id);
        if (listed is null)
        {
            return Found.None("#" + id.TrimStart('#') + " is not on the newest screen");
        }

        var node = listed.Node;
        var tries = MaxTries;
        async Task<Probe?> Attempt(LocatorExpression expression, IReadOnlyList<SemanticNode>? within = null)
        {
            if (tries <= 0)
            {
                return null;
            }

            tries--;
            return await ProbeAsync(expression, listed, within ?? listed.Roots, token).ConfigureAwait(false);
        }

        (LocateQuery Located, Probe Probe)? twins = null;
        SemanticNode? match = null;
        var matchCertain = false;
        var queries = NodeQueries(node, options.Redact);
        foreach (var query in queries)
        {
            var located = options.Query(query);
            var probe = await Attempt(located.Query).ConfigureAwait(false);
            if (probe is null)
            {
                break;
            }

            if (probe.Count == 1 && probe.Index == 0)
            {
                return Found.Of(located);
            }

            if (probe.Index >= 0 && (match is null || (probe.Certain && !matchCertain)))
            {
                match = probe.Matches[probe.Index];
                matchCertain = probe.Certain;
            }

            if (probe.Count > 1 && probe.Index >= 0 && (twins is null || (probe.Certain && !twins.Value.Probe.Certain)))
            {
                twins = (located, probe);
            }
        }

        // The tree an observation hands over carries no test id; the node the locator engine read does, found among the matches of its role.
        if (match is null && node.Role is not null && Roles.Contains(node.Role))
        {
            var probe = await Attempt(options.Query(new LocateArgs { Role = node.Role }).Query).ConfigureAwait(false);
            if (probe is not null && probe.Index >= 0)
            {
                match = probe.Matches[probe.Index];
                matchCertain = probe.Certain;
            }
        }

        // Only a match placed for certain lends its test id: one its box alone placed may be a twin that moved there, and its id would name the twin.
        var testId = !matchCertain || match?.TestId is null ? null : Clean(options.Redact(match.TestId));
        if (testId is not null)
        {
            var located = options.Query(new LocateArgs { TestId = testId });
            var probe = await Attempt(located.Query).ConfigureAwait(false);
            if (probe is not null && probe.Count == 1 && string.Equals(probe.Matches[0].TestId, match!.TestId, StringComparison.Ordinal))
            {
                return Found.Of(located);
            }
        }

        if (twins is not { } pair)
        {
            if (tries <= 0)
            {
                return Found.None("no locator found it alone within " + MaxTries.ToString(CultureInfo.InvariantCulture) + " tries; try locate");
            }

            return Found.None(
                queries.Count == 0 && testId is null
                    ? "the node has no name, text, placeholder, or test id to find it by"
                    : "no locator from its role and name, label, placeholder, text, or test id finds it on the live screen; try locate");
        }

        var visible = VisibleOnly(pair.Located);
        if (visible is not null)
        {
            var probe = await Attempt(visible.Query).ConfigureAwait(false);
            if (probe is { Count: 1, Index: 0 })
            {
                return Found.Of(visible);
            }
        }

        foreach (var scope in ContainerScopes(listed, options.Query))
        {
            var expression = ScopedUnder(pair.Located.Query, scope.Located.Query);
            if (expression is null)
            {
                break;
            }

            var probe = await Attempt(expression, [scope.Within]).ConfigureAwait(false);
            if (probe is null)
            {
                break;
            }

            if (probe is { Count: 1, Index: 0 })
            {
                return Found.Of(new LocateQuery(expression, scope.Located.Code + pair.Located.Code["Screen".Length..], pair.Located.NotFound));
            }
        }

        var (count, index, certain) = (pair.Probe.Count, pair.Probe.Index, pair.Probe.Certain);

        // A position is only as good as the evidence for it: a box from the observation may have moved onto a twin since.
        if (!certain)
        {
            return Found.None("it shares " + pair.Located.Code + " with " + (count - 1).ToString(CultureInfo.InvariantCulture) + " other nodes, and nothing on the screen tells it apart for certain");
        }

        return new Found(
            new LocateQuery(
                new LocatorExpression { Kind = "index", Source = pair.Located.Query, Index = index },
                pair.Located.Code + (index == 0 ? ".First()" : ".Nth(" + index.ToString(CultureInfo.InvariantCulture) + ")"),
                pair.Located.NotFound),
            (index + 1).ToString(CultureInfo.InvariantCulture) + " of " + count.ToString(CultureInfo.InvariantCulture) + " matches, by position; a unique name or test id holds up better",
            null);
    }

    /// <summary>
    /// How many nodes a locator matches now, and which of them is the target. When every match was read and the matches that read like
    /// the target are the twins the observation listed under <paramref name="within"/>, each where it was or all moved together, the
    /// target's place is certain: the twin in its turn, or, when the engine lists them in another order than the observation (light DOM
    /// before shadow roots), the one still in its box. Otherwise it is the one match centered in its box from the observation, as an inner
    /// text element sits inside the button it names, and the place is not certain.
    /// </summary>
    private async Task<Probe> ProbeAsync(LocatorExpression expression, ListedNode listed, IReadOnlyList<SemanticNode> within, CancellationToken token)
    {
        var refs = await options.Resolve(expression, token).ConfigureAwait(false);
        var matches = refs.Take(MaxReadMatches).ToList();
        var count = refs.Count;
        var target = listed.Node;
        if (matches.Count == count)
        {
            var self = IdentityOf(target, options.Redact);
            var alike = matches.Select((match, index) => (match, index)).Where(pair => IdentityOf(pair.match, options.Redact) == self).Select(pair => pair.index).ToList();
            var twins = TwinsOf(within, target, options.Redact);
            var turn = twins.IndexOf(target);

            // A hidden target is no twin of the observation's listing: its turn among them is unknown.
            if (alike.Count == twins.Count && turn >= 0)
            {
                var live = alike.Select(index => matches[index]).ToList();
                if (twins.Count == 1 || MovedTogether(twins, live))
                {
                    return new Probe(count, alike[turn], true, matches);
                }

                var still = alike.Where(index => CenteredIn(matches[index], target)).ToList();
                if (still.Count == 1 && StayedPut(twins, live))
                {
                    return new Probe(count, still[0], true, matches);
                }
            }
        }

        var boxed = matches.Select((match, index) => (match, index)).Where(pair => CenteredIn(pair.match, target)).Select(pair => pair.index).ToList();
        return new Probe(count, boxed.Count == 1 ? boxed[0] : -1, false, matches);
    }

    /// <summary>The node an id names on the newest screen, with its ancestors.</summary>
    private ListedNode? Listed(string id)
    {
        if (_screen is not { } screen || !screen.Nodes.TryGetValue(id.TrimStart('#'), out var entry))
        {
            return null;
        }

        var ancestors = new List<SemanticNode>();
        for (var parent = entry.Parent; parent is not null && screen.Nodes.TryGetValue(parent, out var above); parent = above.Parent)
        {
            if (!Hidden(above.Node))
            {
                ancestors.Add(above.Node);
            }
        }

        return new ListedNode(entry.Node, ancestors, screen.Roots);
    }

    /// <summary>
    /// The queries a node's own fields give, in the order the skill recommends: role and name, then label, placeholder, and text. The test
    /// id is not among an observation's fields; the line search reads it off the live node.
    /// </summary>
    public static IReadOnlyList<LocateArgs> NodeQueries(SemanticNode node, Func<string, string> redact)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(redact);
        var queries = new List<LocateArgs>();
        var name = Clean(node.Name is null ? null : redact(node.Name));
        if (name is not null && node.Role is not null && Roles.Contains(node.Role))
        {
            queries.Add(new LocateArgs { Role = node.Role, Name = name });
        }

        if (name is not null)
        {
            queries.Add(new LocateArgs { Label = name });
        }

        var placeholder = Clean(node.Placeholder is null ? null : redact(node.Placeholder));
        if (placeholder is not null)
        {
            queries.Add(new LocateArgs { Placeholder = placeholder });
        }

        foreach (var text in new[] { Clean(node.Text is null ? null : redact(node.Text)), name }.Where(text => text is not null).Distinct())
        {
            queries.Add(new LocateArgs { Text = text });
        }

        return queries;
    }

    /// <summary>
    /// The <c>App.OpenAsync</c> line for a navigation: a URL on the app's origin as its path, so the line holds on the port the next run
    /// gets, the base URL itself as <c>App.OpenAsync()</c>, anything else as given.
    /// </summary>
    public static string NavigationLine(string url, AppBase? appBase)
    {
        if (appBase is not null && Uri.TryCreate(appBase.Href, UriKind.Absolute, out var root) && Uri.TryCreate(root, url, out var resolved))
        {
            if (string.Equals(resolved.AbsoluteUri, appBase.Href, StringComparison.Ordinal))
            {
                return "await App.OpenAsync();";
            }

            if (string.Equals(resolved.GetLeftPart(UriPartial.Authority), appBase.Origin, StringComparison.Ordinal))
            {
                return "await App.OpenAsync(" + SessionCatalog.Quote(resolved.AbsolutePath + resolved.Query + resolved.Fragment) + ");";
            }
        }

        return "await App.OpenAsync(" + SessionCatalog.Quote(url) + ");";
    }

    /// <summary>
    /// The containers a locator can be scoped under to tell the target from its twins, nearest first: an ancestor with a role, by its
    /// name, or by the first text it holds outside the target, the way a test scopes the "Delete" button of one row.
    /// </summary>
    private List<(LocateQuery Located, SemanticNode Within)> ContainerScopes(ListedNode listed, Func<LocateArgs, LocateQuery> query)
    {
        var scopes = new List<(LocateQuery, SemanticNode)>();
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
            var name = Clean(ancestor.Name is null ? null : options.Redact(ancestor.Name));
            if (name is not null)
            {
                scopes.Add((query(new LocateArgs { Role = role, Name = name }), ancestor));
            }

            var text = FirstText(ancestor, listed.Node.Ref) ?? Clean(ancestor.Text is null ? null : options.Redact(ancestor.Text));
            if (text is not null && !string.Equals(text, name, StringComparison.Ordinal))
            {
                var root = query(new LocateArgs { Role = role });
                scopes.Add((
                    new LocateQuery(new LocatorExpression { Kind = "filter", Source = root.Query, HasText = text }, root.Code + ".Filter(" + SessionCatalog.Quote(text) + ")", root.NotFound),
                    ancestor));
            }
        }

        return scopes;
    }

    /// <summary>The first text a container holds, depth first, outside the subtree of <paramref name="skip"/>.</summary>
    private string? FirstText(SemanticNode node, string skip)
    {
        foreach (var child in node.Children)
        {
            if (string.Equals(child.Ref, skip, StringComparison.Ordinal) || Hidden(child))
            {
                continue;
            }

            if (child.Children.Count == 0)
            {
                var own = Clean(child.Text is null ? null : options.Redact(child.Text)) ?? Clean(child.Name is null ? null : options.Redact(child.Name));
                if (own is not null)
                {
                    return own;
                }

                continue;
            }

            var deeper = FirstText(child, skip);
            if (deeper is not null)
            {
                return deeper;
            }
        }

        return null;
    }

    /// <summary>
    /// A bare query kept to the nodes that render, the way a test steps past a hidden twin (<c>Visible = true</c>); null for anything else,
    /// or a query whose code already carries options.
    /// </summary>
    private static LocateQuery? VisibleOnly(LocateQuery located)
    {
        var expression = located.Query;
        if (expression.Kind is "filter" or "index" || expression.Parent is not null || located.Code.Contains("new ", StringComparison.Ordinal) || located.Code.Contains("exact:", StringComparison.Ordinal))
        {
            return null;
        }

        var visible = expression.Kind == "role" ? "new RoleOptions { Visible = true }" : "new TextMatchOptions { Visible = true }";
        return new LocateQuery(expression with { Visible = true }, located.Code[..^1] + ", " + visible + ")", located.NotFound);
    }

    /// <summary>A query expression scoped under another expression; null for an expression that is not a bare query.</summary>
    private static LocatorExpression? ScopedUnder(LocatorExpression child, LocatorExpression scope) =>
        child.Kind is not ("filter" or "index") && child.Parent is null ? child with { Parent = scope } : null;

    /// <summary>Whether a node the locator engine read sits with its center inside the target's box from the observation.</summary>
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

    /// <summary>What a node reads as: its role, name, text, and placeholder, in the one-line form, after the secret ledger.</summary>
    private static string IdentityOf(SemanticNode node, Func<string, string> redact)
    {
        string Field(string? value) => Clean(value is null ? null : redact(value)) ?? string.Empty;
        return string.Join('\0', node.Role ?? string.Empty, Field(node.Name), Field(node.Text), Field(node.Placeholder));
    }

    /// <summary>The nodes under <paramref name="within"/> that read like the target, in the observation's order, the target among them.</summary>
    private static List<SemanticNode> TwinsOf(IReadOnlyList<SemanticNode> within, SemanticNode target, Func<string, string> redact)
    {
        var self = IdentityOf(target, redact);
        var twins = new List<SemanticNode>();
        void Walk(SemanticNode node)
        {
            if (!Hidden(node) && IdentityOf(node, redact) == self)
            {
                twins.Add(node);
            }

            foreach (var child in node.Children)
            {
                Walk(child);
            }
        }

        foreach (var root in within)
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
            if (observed[index].Rect is not { } was || index >= live.Count || live[index].Rect is not { } now)
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
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var match in live)
        {
            var boxes = observed.Where(twin => CenteredIn(match, twin)).ToList();
            if (boxes.Count != 1 || !taken.Add(boxes[0].Ref))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Hidden(SemanticNode node) => node.States.Hidden || node.States.AriaHidden;

    /// <summary>Indexes an observation's tree by id, each node with its parent's id.</summary>
    private static Screen IndexScreen(Observation observation)
    {
        var nodes = new Dictionary<string, (SemanticNode Node, string? Parent)>(StringComparer.Ordinal);
        void Walk(SemanticNode node, string? parent)
        {
            nodes[node.Ref] = (node, parent);
            foreach (var child in node.Children)
            {
                Walk(child, node.Ref);
            }
        }

        foreach (var root in observation.Roots)
        {
            Walk(root, null);
        }

        return new Screen(observation.Roots, nodes);
    }

    /// <summary>Text in the one-line form a locator matches against, or null for none.</summary>
    private static string? Clean(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var collapsed = TextRules.Collapse(text);
        return collapsed.Length == 0 ? null : collapsed;
    }

    /// <summary>A string argument of a tool call, or null.</summary>
    private static string? StringField(JsonElement input, string key) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>One recorded action: its line, or why it has none.</summary>
    private sealed record Entry(string Tool, string? Line, string? Reason);

    /// <summary>What an action prints: the line, or why there is none.</summary>
    private sealed record LinePlan(string? Line, string? Reason)
    {
        public static LinePlan Of(string line) => new(line, null);

        public static LinePlan None(string reason) => new(null, reason);
    }

    /// <summary>What a locator search found: the locator and how the line comments on it, or why there is none.</summary>
    private sealed record Found(LocateQuery? Located, string? Comment, string? Reason)
    {
        public string? Code => Located?.Code;

        public static Found Of(LocateQuery located) => new(located, null, null);

        public static Found None(string reason) => new(null, null, reason);
    }

    /// <summary>A node the newest observation listed, with its ancestors, nearest first, and the screen's roots.</summary>
    private sealed record ListedNode(SemanticNode Node, IReadOnlyList<SemanticNode> Ancestors, IReadOnlyList<SemanticNode> Roots);

    /// <summary>The newest observation's tree, and its nodes by id, each with its parent's id.</summary>
    private sealed record Screen(IReadOnlyList<SemanticNode> Roots, IReadOnlyDictionary<string, (SemanticNode Node, string? Parent)> Nodes);

    /// <summary>What one locator matched: how many nodes, which of them is the target (-1 when none read is placed as it), whether that place is certain, and every match read.</summary>
    private sealed record Probe(int Count, int Index, bool Certain, IReadOnlyList<SemanticNode> Matches);
}
