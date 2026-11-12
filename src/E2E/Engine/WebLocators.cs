// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// A text query's pattern with its exactness (upstream's <c>TextPattern</c>, <c>e2e/engine</c>). Upstream's pattern carries its own
/// <c>exact</c>; the port's <see cref="TextMatch"/> does not, so the expression does (<see cref="LocatorExpression.Exact"/>) and this pairs
/// the two. .NET-only shape.
/// </summary>
internal readonly record struct TextPattern(TextMatch Match, bool Exact);

/// <summary>What a projection reads besides the expression (upstream's <c>ProjectionOptions</c>).</summary>
/// <param name="TestIdAttribute">The attribute a <c>testId</c> query reads.</param>
/// <param name="FrameHidden">True when a frame along the expression's frame chain is hidden, so every node in it is.</param>
/// <param name="IncludeHidden">
/// Whether a role query also matches hidden nodes (<see cref="LocatorExpression.IncludeHidden"/>, which <c>toBeAttached</c> and
/// <c>waitFor</c> set). .NET-only: upstream's engines have no such switch.
/// </param>
internal sealed record ProjectionOptions(string TestIdAttribute, bool FrameHidden, bool IncludeHidden = false);

/// <summary>
/// One step a display-value projection applies after its candidates are value-filtered, in expression order: a position among the matches,
/// or a per-element filter that came after a position and so could not compose onto the candidate locator (upstream's <c>PostStep</c>).
/// </summary>
internal abstract record PostStep
{
    /// <summary>A position among the matches (upstream's <c>kind: 'index'</c>): 0 or more is <c>nth</c> (0 is <c>first</c>), <see cref="LocatorExpression.LastIndex"/> is <c>last</c>.</summary>
    public sealed record Position(int Index) : PostStep;

    /// <summary>A per-element filter (upstream's <c>kind: 'filter'</c>).</summary>
    public sealed record Filter(LocatorFilterOptions Options) : PostStep;
}

/// <summary>The result of projecting an expression (upstream's <c>ProjectedLocator</c>).</summary>
internal sealed record ProjectedLocator
{
    /// <summary>
    /// The Playwright locator to resolve. For a display-value query this is the candidate set (every form control in scope), not the
    /// matches: Playwright has no selector for a control's current value, so the value predicate can only run after the candidates are read.
    /// </summary>
    public required ILocator Locator { get; init; }

    /// <summary>Non-null when the terminal query filters by display value.</summary>
    public TextPattern? DisplayValue { get; init; }

    /// <summary>
    /// Non-null when the terminal query is an exact label query: <see cref="Locator"/> holds the controls the <c>e2e-label</c> engine found
    /// labelled with the pattern, and the engine reads them and keeps those still labelled with it: an associated label (an
    /// <c>aria-label</c>, an <c>aria-labelledby</c> target, or a <c>&lt;label&gt;</c>) whose accessible-name text equals the pattern.
    /// Playwright's own <c>getByLabel</c> reads a label's full text, aria-hidden included, so a required-field marker made
    /// <c>getByLabel('Display name')</c> miss <c>Display name*</c>; the engine's reader drops such text.
    /// </summary>
    public TextPattern? Name { get; init; }

    /// <summary>
    /// The Playwright locator to compose with as a scope or <c>has</c> filter, for a projection whose own predicate lives outside Playwright's
    /// chain. An exact label query composes as its <see cref="Locator"/>, whose <c>e2e-label</c> engine runs the reader's label predicate
    /// inside the page, so a <c>has</c> filter keeps the row the label names and no row whose label merely contains it. Null for a
    /// display-value projection, which has no such equivalent and is rejected instead.
    /// </summary>
    public ILocator? Composable { get; init; }

    /// <summary>
    /// Steps to apply to the value-filtered matches, innermost first. Always empty when <see cref="DisplayValue"/> is null, because the
    /// projection then composes positions and filters natively onto the locator.
    /// </summary>
    public IReadOnlyList<PostStep> Steps { get; init; } = [];

    /// <summary>
    /// True when the terminal query keeps only nodes whose hidden state is false. The engine applies it to the batch read before the
    /// display-value predicate and any post step, so a position is taken among shown matches.
    /// </summary>
    public bool Visible { get; init; }
}

/// <summary>
/// What the web engine reads off a <see cref="LocatorExpression"/> before it resolves it, and how it projects an expression onto a
/// Playwright locator chain over the selector engines (upstream's <c>web/src/locators.ts</c>).
/// </summary>
internal static class WebLocators
{
    // The contract's role vocabulary spelled as the ARIA role the role selector knows, where the two differ: the tree reports an img as image,
    // so an image query has to go back to img to match it (upstream's ARIA_ROLE_BY_CONTRACT_ROLE).
    private static readonly Dictionary<string, string> AriaRoleByContractRole = new(StringComparer.Ordinal) { ["image"] = "img" };

    // Every control that displays a current value: the candidates of a display-value query. A checkbox or radio carries a value it never
    // shows (on by default), so it is no candidate (upstream's VALUED_SELECTOR).
    private const string ValuedSelector = "input:not([type=\"checkbox\" i]):not([type=\"radio\" i]), textarea, select";

    // Whitespace or an icon-font glyph (a private-use code point), the characters the tree's names drop or collapse (upstream's GLYPH_OR_SPACE).
    private const string GlyphOrSpace = @"[\s\p{Co}]";

    // Matches no element. Inside a hidden frame every node reads as hidden, which the frame's own document cannot see, so a query that never
    // matches a hidden node, a visible or a role query, narrows to it there (upstream's NOTHING_SELECTOR).
    private const string NothingSelector = "css=:not(*)";

    private const string DisplayValueCompositionMessage =
        "displayValue queries cannot scope child queries or serve as a has or hasNot filter in this engine";

    // Playwright's own scope: the page, a frame locator, or a locator (upstream's PwScope).
    private readonly struct PwScope
    {
        private readonly object _scope;

        private PwScope(object scope) => _scope = scope;

        public static PwScope Of(ILocator locator) => new(locator);

        public static PwScope Of(IPage page) => new(page);

        public static PwScope Of(IFrameLocator frame) => new(frame);

        public ILocator Locator(string selector) => _scope switch
        {
            IPage page => page.Locator(selector),
            IFrameLocator frame => frame.Locator(selector),
            _ => ((ILocator)_scope).Locator(selector),
        };

        public PwScope FrameLocator(string selector) => _scope switch
        {
            IPage page => Of(page.FrameLocator(selector)),
            IFrameLocator frame => Of(frame.FrameLocator(selector)),
            _ => Of(((ILocator)_scope).FrameLocator(selector)),
        };
    }

    // How one query kind reaches its nodes across the roots it searches: its scope, then every closed shadow root recorded under it. A
    // Playwright getter is composed onto those roots (e2e-roots=), so the platform's own role, name, text, label, and attribute matching runs
    // in each of them. A CSS candidate set is matched per root by the engine itself (e2e-roots=<css>) and narrowed by a predicate the engine
    // applies once the candidates are read: a control's current value. The engine matches the CSS itself rather than composing it onto the
    // roots because Playwright's CSS engine sorts a list's matches in DOM order through shadowRoot, which a closed root does not expose, and
    // drops the ones it cannot place. An engine selector names one of ours that searches the scope and its closed roots on its own (upstream's
    // Candidates).
    private abstract record Candidates
    {
        public sealed record Playwright(Func<ILocator, ILocator> Locate) : Candidates;

        public sealed record Css(string Selector) : Candidates;

        public sealed record Engine(string Selector) : Candidates;
    }

    /// <summary>
    /// A text pattern as Playwright takes it: a string, or a regular expression carrying only the flags Playwright can send
    /// (<see cref="RegexOptions.IgnoreCase"/>, <see cref="RegexOptions.Multiline"/>, <see cref="RegexOptions.Singleline"/>).
    /// </summary>
    private static Regex Sendable(Regex pattern)
    {
        const RegexOptions Sendable = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline;
        return (pattern.Options & ~Sendable) == 0 ? pattern : new Regex(pattern.ToString(), pattern.Options & Sendable, pattern.MatchTimeout);
    }

    /// <summary>
    /// A <see cref="Regex"/> as Playwright writes a RegExp into a selector: <c>/source/flags</c> with the quotes and <c>&gt;&gt;</c> escaped
    /// (upstream's <c>escapeRegexForSelector</c> over <c>String(re)</c>). The flags <c>u</c> and <c>v</c>, which a .NET <see cref="Regex"/>
    /// cannot carry, are written by <see cref="GlyphTolerantName"/> as a string.
    /// </summary>
    private static string RegexForSelector(Regex pattern)
    {
        var flags = new StringBuilder();
        if ((pattern.Options & RegexOptions.IgnoreCase) != 0)
        {
            flags.Append('i');
        }

        if ((pattern.Options & RegexOptions.Multiline) != 0)
        {
            flags.Append('m');
        }

        if ((pattern.Options & RegexOptions.Singleline) != 0)
        {
            flags.Append('s');
        }

        // JavaScript writes an unescaped slash of a source as \/.
        // A slash, quote, or backtick that no backslash escapes (one preceded by an even number of them).
        const string Unescaped = @"(?<=(?:^|[^\\])(?:\\\\)*)";
        var source = Regex.Replace(pattern.ToString(), Unescaped + "/", "\\/");
        var literal = "/" + source + "/" + flags;
        return Regex.Replace(literal, Unescaped + "([\"'`])", "\\$1").Replace(">>", "\\>\\>", StringComparison.Ordinal);
    }

    /// <summary>
    /// A role query's string name as a pattern for the name the browser computes, which keeps the private-use glyphs of an icon font the
    /// tree drops (<c>in-page/read-semantics.ts</c>): "Login" matches the browser's " Login", the button a person reads as Login. A
    /// glyph may stand wherever the name has a space or begins or ends. Exact is whole and case-sensitive, else a case-insensitive
    /// substring, as Playwright matches a string name. Upstream's <c>glyphTolerantName</c> returns a <c>RegExp</c> with the <c>u</c> flag;
    /// Microsoft.Playwright cannot send that flag for a .NET <see cref="Regex"/>, so this is the regular expression written as a selector
    /// attribute value (<c>/source/u</c>), with upstream's <c>selectorSafeRegExp</c> applied: a quote or the second <c>&gt;</c> of
    /// <c>&gt;&gt;</c> becomes the hex escape the flag accepts. Playwright escapes both itself, except under <c>u</c> or <c>v</c>, where a
    /// backslash-quote is a syntax error and a bare quote opens a string that swallows every part chained after it.
    /// </summary>
    internal static string GlyphTolerantName(string name, bool exact)
    {
        var body = string.Join(
            GlyphOrSpace + "+",
            Regex.Split(name.Trim(), @"\s+").Select(word => Regex.Replace(word, @"[.*+?^${}()|\[\]\\/]", "\\$&")));
        var source = exact ? "^" + GlyphOrSpace + "*" + body + GlyphOrSpace + "*$" : body;
        source = Regex.Replace(source, "[\"'`]", match => "\\x" + ((int)match.Value[0]).ToString("x", CultureInfo.InvariantCulture))
            .Replace(">>", ">\\x3e", StringComparison.Ordinal);
        return "/" + source + (exact ? "/u" : "/iu");
    }

    /// <summary>
    /// The selector <c>getByTestId</c> compiles to, with the project's attribute in place of Playwright's process-global one
    /// (<c>selectors.setTestIdAttribute</c> would leak one engine's attribute into every other engine of the process). The value is encoded as
    /// Playwright's <c>escapeForAttributeSelector</c> encodes it: a string is an exact, case-sensitive match; a RegExp is passed as written.
    /// </summary>
    private static string TestIdSelector(string attribute, TextPattern pattern)
    {
        var name = attribute.Contains(',', StringComparison.Ordinal) ? System.Text.Json.JsonSerializer.Serialize(attribute) : attribute;
        var value = pattern.Match.Pattern is { } regex
            ? RegexForSelector(regex)
            : "\"" + pattern.Match.Text!.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"s";
        return $"internal:testid=[{name}={value}]";
    }

    // The role selector getByRole compiles to (Playwright's getByRoleSelector), written here because a string name is a pattern with the u
    // flag, which Microsoft.Playwright cannot send through getByRole: the properties in Playwright's order, then name.
    private static string RoleSelector(LocatorExpression query, TextMatch? name, bool exact, bool includeHidden)
    {
        var role = AriaRoleByContractRole.GetValueOrDefault(query.Role!, query.Role!);
        var selector = new StringBuilder("internal:role=").Append(role);
        void Add(string key, bool? state)
        {
            if (state is bool value)
            {
                selector.Append('[').Append(key).Append('=').Append(value ? "true" : "false").Append(']');
            }
        }

        Add("checked", query.Checked);
        Add("disabled", query.Disabled);
        Add("selected", query.Selected);
        Add("expanded", query.Expanded);
        if (includeHidden)
        {
            selector.Append("[include-hidden=true]");
        }

        if (query.Level is int level)
        {
            selector.Append("[level=").Append(level.ToString(CultureInfo.InvariantCulture)).Append(']');
        }

        if (name is not null)
        {
            selector.Append("[name=").Append(name.Pattern is { } regex ? RegexForSelector(regex) : GlyphTolerantName(name.Text!, exact)).Append(']');
        }

        Add("pressed", query.Pressed);
        return selector.ToString();
    }

    /// <summary>
    /// The one table of query kinds: each declares its Playwright getter or its CSS candidate set. An exact label query is the one kind whose
    /// candidates depend on the pattern: exact goes to the <c>e2e-label</c> engine, substring stays with Playwright's <c>getByLabel</c>.
    /// </summary>
    private static Candidates CandidatesOf(LocatorExpression query, ProjectionOptions options)
    {
        var pattern = query.Value is null ? default : new TextPattern(query.Value, query.Exact);
        switch (query.Kind)
        {
            case "role":
                return new Candidates.Playwright(roots => roots.Locator(RoleSelector(query, query.Value, query.Exact, options.IncludeHidden)));
            case "label":
                return query.Value!.Pattern is null && query.Exact
                    ? new Candidates.Engine(LabelSelector.ExactLabelSelector(new ExactLabelSelectorBody(query.Value.Text!, options.TestIdAttribute, ReadNode.SecureFieldSelector)))
                    : new Candidates.Playwright(roots => query.Value.Pattern is { } regex
                        ? roots.GetByLabel(Sendable(regex), new LocatorGetByLabelOptions { Exact = false })
                        : roots.GetByLabel(query.Value.Text!, new LocatorGetByLabelOptions { Exact = false }));
            case "placeholder":
                return new Candidates.Playwright(roots => pattern.Match.Pattern is { } regex
                    ? roots.GetByPlaceholder(Sendable(regex), new LocatorGetByPlaceholderOptions { Exact = false })
                    : roots.GetByPlaceholder(pattern.Match.Text!, new LocatorGetByPlaceholderOptions { Exact = pattern.Exact }));
            case "text":
                return new Candidates.Playwright(roots => pattern.Match.Pattern is { } regex
                    ? roots.GetByText(Sendable(regex), new LocatorGetByTextOptions { Exact = false })
                    : roots.GetByText(pattern.Match.Text!, new LocatorGetByTextOptions { Exact = pattern.Exact }));
            case "displayValue":
                return new Candidates.Css(ValuedSelector);
            case "testid":
                return new Candidates.Playwright(roots => roots.Locator(TestIdSelector(options.TestIdAttribute, pattern)));
            default:
                throw new EngineException("ENGINE_FAILURE", $"a {query.Kind} expression is no query", retryable: false);
        }
    }

    /// <summary>Composes the candidates of one query onto <paramref name="scope"/> and the closed shadow roots under it.</summary>
    private static ILocator QueryToPw(PwScope scope, LocatorExpression query, ProjectionOptions options) => CandidatesOf(query, options) switch
    {
        Candidates.Css css => scope.Locator($"{PageScript.SearchRootsSelectorEngine}={css.Selector}"),
        Candidates.Engine engine => scope.Locator(engine.Selector),
        Candidates.Playwright playwright => playwright.Locate(scope.Locator($"{PageScript.SearchRootsSelectorEngine}=")),
        _ => throw new InvalidOperationException(),
    };

    /// <summary>
    /// A <c>visible</c> query narrows its candidates inside the selector, before any enclosing scope, filter, or index runs, by the reader's own
    /// hidden state (<c>e2e-shown</c>). An indexed, filtered, or scoping visible query therefore never selects or retains a node that state calls
    /// hidden, and the engine's hold of a terminal query to the batch-read state agrees with it.
    /// </summary>
    private static ILocator VisibleQueryToPw(PwScope scope, LocatorExpression query, ProjectionOptions options) =>
        NarrowedToVisible(QueryToPw(scope, query, options), query, options.FrameHidden, options.IncludeHidden);

    /// <summary>The visibility narrowing above, on a locator already built for <paramref name="query"/>; identity unless the query keeps shown nodes alone.</summary>
    private static ILocator NarrowedToVisible(ILocator located, LocatorExpression query, bool frameHidden, bool includeHidden)
    {
        // A role query matches a hidden node only when the expression asks for it (IncludeHidden, .NET-only: toBeAttached and waitFor).
        if (frameHidden && (query.Visible || (query.Kind == "role" && !includeHidden)))
        {
            return located.Locator(NothingSelector);
        }

        return query.Visible ? located.Locator(ShownSelector.Selector) : located;
    }

    private static ILocator Positioned(ILocator locator, int index) =>
        index == LocatorExpression.LastIndex ? locator.Last : index == 0 ? locator.First : locator.Nth(index);

    /// <summary>A projection whose matches are exactly <paramref name="locator"/>'s, with nothing left for the engine to apply.</summary>
    private static ProjectedLocator Plain(ILocator locator) => new() { Locator = locator };

    private static void SetHasText(LocatorFilterOptions filter, TextMatch? match, bool not)
    {
        switch (match)
        {
            case null:
                return;
            case { Pattern: { } regex } when not:
                filter.HasNotTextRegex = Sendable(regex);
                return;
            case { Pattern: { } regex }:
                filter.HasTextRegex = Sendable(regex);
                return;
            case var text when not:
                filter.HasNotText = text.Text;
                return;
            default:
                filter.HasText = match.Text;
                return;
        }
    }

    /// <summary>
    /// Projects a complete immutable expression onto a Playwright locator within one scope. Frame cardinality is validated separately by the
    /// engine.
    /// </summary>
    /// <remarks>
    /// Every semantic query searches its scope and the closed shadow roots under it (see <c>Candidates</c>), so a scope, a <c>has</c> filter,
    /// and an index reach across a closed boundary. Two compositions stay Playwright's own and stop at one: a <c>selector</c> expression is the
    /// platform's CSS or XPath, and <c>filter({ hasText, hasNotText })</c> reads an element's text as Playwright does, light DOM and open
    /// roots, so text inside a closed root does not count toward an element outside it.
    /// A display-value query resolves to a candidate locator plus a value predicate the engine applies once the candidates are read.
    /// Per-element filters (<c>filter({ hasText, hasNotText, has, hasNot })</c>) commute with that predicate, so they compose onto the
    /// candidate locator as for any other query. Positional selection does not commute (positions are relative to the value-filtered matches),
    /// so it is recorded as a step and applied after filtering; a filter that follows a position is recorded the same way and checked on the
    /// selected element. Two compositions need the predicate inside Playwright's own chain and stay unsupported: a display-value query as the
    /// scope of a child query, and as a <c>has</c> filter.
    /// </remarks>
    private static ProjectedLocator Project(PwScope scope, LocatorExpression expression, ProjectionOptions options)
    {
        switch (expression.Kind)
        {
            case "union":
                {
                    var left = RequireComposable(Project(scope, expression.Left!, options));
                    var right = RequireComposable(Project(scope, expression.Right!, options));
                    return Plain(left.Or(right));
                }

            case "filter":
                {
                    var source = Project(scope, expression.Source!, options);
                    var filter = new LocatorFilterOptions();
                    SetHasText(filter, expression.HasText, not: false);
                    SetHasText(filter, expression.HasNotText, not: true);
                    if (expression.Has is not null)
                    {
                        filter.Has = RequireComposable(Project(scope, WithinSource(expression.Has, expression.Source!, "has"), options));
                    }

                    if (expression.HasNot is not null)
                    {
                        filter.HasNot = RequireComposable(Project(scope, WithinSource(expression.HasNot, expression.Source!, "hasNot"), options));
                    }

                    if (source.Steps.Count > 0)
                    {
                        return source with { Steps = [.. source.Steps, new PostStep.Filter(filter)] };
                    }

                    return source with
                    {
                        Locator = source.Locator.Filter(filter),
                        Composable = source.Composable?.Filter(filter),
                        Steps = [],
                    };
                }

            case "index":
                {
                    var source = Project(scope, expression.Source!, options);
                    var index = expression.Index!.Value;
                    if (source.DisplayValue is not null || source.Name is not null)
                    {
                        return source with
                        {
                            Composable = source.Composable is null ? null : Positioned(source.Composable, index),
                            Steps = [.. source.Steps, new PostStep.Position(index)],
                        };
                    }

                    return Plain(Positioned(source.Locator, index));
                }

            case "selector":
                return Plain(scope.Locator(expression.Selector!));
            case "frame":
                return Project(scope.FrameLocator(expression.Selector!), expression.Source!, options);
            default:
                {
                    var inner = expression.Parent is null ? scope : PwScope.Of(RequireComposable(Project(scope, expression.Parent, options)));
                    var exactLabel = expression.Kind == "label" && expression.Value is { Pattern: null } && expression.Exact
                        ? new TextPattern(expression.Value, expression.Exact)
                        : (TextPattern?)null;
                    var locator = VisibleQueryToPw(inner, expression, options);
                    return new ProjectedLocator
                    {
                        Locator = locator,
                        DisplayValue = expression.Kind == "displayValue" ? new TextPattern(expression.Value!, expression.Exact) : null,
                        Name = exactLabel,
                        Composable = exactLabel is null ? null : locator,
                        Visible = expression.Visible,
                    };
                }
        }
    }

    /// <summary>Projects an expression onto the page (upstream's <c>projectExpression</c>).</summary>
    public static ProjectedLocator ProjectExpression(IPage page, LocatorExpression expression, ProjectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(options);
        return Project(PwScope.Of(page), expression, options);
    }

    /// <summary>
    /// The locator of a projection that must participate in Playwright's own locator chain: a scope for child queries or a <c>has</c> target.
    /// A display-value projection cannot, because its value predicate lives outside that chain (upstream's <c>requireComposable</c>).
    /// </summary>
    private static ILocator RequireComposable(ProjectedLocator projected)
    {
        if (projected.Composable is not null)
        {
            return projected.Composable;
        }

        if (projected.DisplayValue is not null)
        {
            throw new EngineException("UNSUPPORTED_CAPABILITY", DisplayValueCompositionMessage, retryable: false);
        }

        return projected.Locator;
    }

    /// <summary>
    /// Applies post steps, innermost first, to an ordered list of matches (upstream's <c>applyPostSteps</c>). <c>nth</c> past the end selects
    /// nothing, matching Playwright's <c>nth</c> on a locator with too few matches. A filter step keeps the matches
    /// <paramref name="passesFilter"/> accepts; it runs only on the matches that survived the steps before it.
    /// </summary>
    public static async Task<IReadOnlyList<T>> ApplyPostSteps<T>(
        IReadOnlyList<T> matches,
        IReadOnlyList<PostStep> steps,
        Func<T, LocatorFilterOptions, Task<bool>> passesFilter)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(passesFilter);
        var selected = matches;
        foreach (var step in steps)
        {
            if (step is PostStep.Position position)
            {
                var index = position.Index;
                selected = index == 0 ? selected.Take(1).ToList()
                    : index == LocatorExpression.LastIndex ? selected.Skip(Math.Max(0, selected.Count - 1)).ToList()
                    : index < selected.Count ? [selected[index]] : [];
                continue;
            }

            var kept = new List<T>();
            foreach (var match in selected)
            {
                if (await passesFilter(match, ((PostStep.Filter)step).Options).ConfigureAwait(false))
                {
                    kept.Add(match);
                }
            }

            selected = kept;
        }

        return selected;
    }

    /// <summary>
    /// Every frame selector along an expression, outermost first (upstream's <c>frameSelectors</c>). The two branches of a <c>union</c>
    /// must share their chain, or the engine cannot join them in one document.
    /// </summary>
    public static List<string> FrameSelectors(LocatorExpression expression)
    {
        switch (expression.Kind)
        {
            case "union":
                {
                    var left = FrameSelectors(expression.Left!);
                    var right = FrameSelectors(expression.Right!);
                    if (!left.SequenceEqual(right, StringComparer.Ordinal))
                    {
                        throw new EngineException("UNSUPPORTED_CAPABILITY", "locator.or requires both branches in the same document", retryable: false);
                    }

                    return left;
                }

            case "filter" or "index":
                return FrameSelectors(expression.Source!);
            case "selector":
                return [];
            case "frame":
                return [expression.Selector!, .. FrameSelectors(expression.Source!)];
            default:
                return expression.Parent is null ? [] : FrameSelectors(expression.Parent);
        }
    }

    /// <summary>
    /// A <c>has</c> filter resolves inside each match of its source, so a frame the two share is already the source's: the frame's iframe
    /// would otherwise scope the filter to descendants of the iframe, which the match is not one of. A <c>has</c> in another document
    /// cannot be joined to its source. Port-only: upstream projects the filter's locator as it is; the port keeps the frame-scoped
    /// <c>has</c> it resolved over its tree.
    /// </summary>
    private static LocatorExpression WithinSource(LocatorExpression has, LocatorExpression source, string key)
    {
        if (!FrameSelectors(has).SequenceEqual(FrameSelectors(source), StringComparer.Ordinal))
        {
            throw new EngineException("UNSUPPORTED_CAPABILITY", $"locator.filter({{ {key} }}) requires both locators in the same document", retryable: false);
        }

        return WithoutFrameScopes(has);
    }

    /// <summary>
    /// The expression with the frame scopes of its queries taken off, for a filter that resolves inside the matches of a source that has
    /// them. A <c>selector</c> keeps its frame: the page decides which elements it names.
    /// </summary>
    private static LocatorExpression WithoutFrameScopes(LocatorExpression expression)
    {
        switch (expression.Kind)
        {
            case "frame":
                {
                    var leaf = expression.Source!;
                    while (leaf.Kind == "frame")
                    {
                        leaf = leaf.Source!;
                    }

                    return leaf.Kind == "selector" ? expression : leaf;
                }

            case "union":
                return expression with { Left = WithoutFrameScopes(expression.Left!), Right = WithoutFrameScopes(expression.Right!) };
            case "filter":
                return expression with { Source = WithoutFrameScopes(expression.Source!), Has = expression.Has is null ? null : WithoutFrameScopes(expression.Has), HasNot = expression.HasNot is null ? null : WithoutFrameScopes(expression.HasNot) };
            case "index":
                return expression with { Source = WithoutFrameScopes(expression.Source!) };
            default:
                return expression.Parent is null ? expression : expression with { Parent = WithoutFrameScopes(expression.Parent) };
        }
    }

    /// <summary>
    /// Refuses a selector that captures with <c>*</c> before its last part. Upstream reads every match through a selector engine
    /// appended to the locator, which a capture earlier in the chain leaves with the wrong element, and refuses it then; the port has no
    /// such read, so it refuses the spelling.
    /// </summary>
    public static void RefuseCapture(string selector)
    {
        var parts = SelectorParts(selector);
        for (var i = 0; i < parts.Count - 1; i++)
        {
            if (parts[i].TrimStart().StartsWith('*'))
            {
                throw new TestException(
                    "INVALID_LOCATOR",
                    "a selector that captures with * (*css=article >> text=Hello) is not supported: use Filter(Locator) instead");
            }
        }
    }

    /// <summary>The parts of a Playwright selector, which <c>&gt;&gt;</c> chains outside any quotes.</summary>
    private static List<string> SelectorParts(string selector)
    {
        var parts = new List<string>();
        var start = 0;
        char? quote = null;
        for (var i = 0; i < selector.Length; i++)
        {
            var c = selector[i];
            if (quote is { } open)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == open)
                {
                    quote = null;
                }

                continue;
            }

            if (c is '"' or '\'' or '`')
            {
                quote = c;
            }
            else if (c == '>' && i + 1 < selector.Length && selector[i + 1] == '>')
            {
                parts.Add(selector[start..i]);
                i++;
                start = i + 1;
            }
        }

        parts.Add(selector[start..]);
        return parts;
    }
}
