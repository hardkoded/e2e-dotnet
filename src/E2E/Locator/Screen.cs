// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// Semantic queries against the current screen. An action or a single-node
/// assertion fails when the query matches more than one node. Narrow it with
/// <see cref="Locator.Filter"/>, <see cref="Locator.First"/>, or <see cref="Locator.Nth"/>.
/// </summary>
public sealed class Screen
{
    private readonly Func<CancellationToken, Task<Observation>> _observe;
    private readonly Func<SemanticNode, LocatorAction, CancellationToken, Task> _perform;
    private readonly Func<CancellationToken> _cancellation;
    private readonly Action _verified;

    internal Screen(
        Func<CancellationToken, Task<Observation>> observe,
        Func<SemanticNode, LocatorAction, CancellationToken, Task> perform,
        Func<CancellationToken> cancellation,
        Action verified,
        TimeSpan actionTimeout,
        TimeSpan assertionTimeout)
    {
        _observe = observe;
        _perform = perform;
        _cancellation = cancellation;
        _verified = verified;
        ActionTimeout = actionTimeout;
        AssertionTimeout = assertionTimeout;
    }

    internal TimeSpan ActionTimeout { get; }

    internal TimeSpan AssertionTimeout { get; }

    internal TimeSpan PollInterval { get; } = TimeSpan.FromMilliseconds(50);

    public Locator GetByRole(string role, string? name = null, bool exact = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return new Locator(this, new LocatorQuery
        {
            Kind = "role",
            Role = role,
            Name = name,
            Exact = exact,
        });
    }

    public Locator GetByText(string text, bool exact = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new Locator(this, new LocatorQuery { Kind = "text", Text = text, Exact = exact });
    }

    public Locator GetByLabel(string label, bool exact = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return new Locator(this, new LocatorQuery { Kind = "label", Name = label, Exact = exact });
    }

    public Locator GetByTestId(string testId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testId);
        return new Locator(this, new LocatorQuery { Kind = "testid", TestId = testId });
    }

    public Locator GetByPlaceholder(string placeholder, bool exact = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placeholder);
        return new Locator(this, new LocatorQuery { Kind = "placeholder", Name = placeholder, Exact = exact });
    }

    internal Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        return _observe(cancellationToken == default ? _cancellation() : cancellationToken);
    }

    internal Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
    {
        return _perform(node, action, cancellationToken == default ? _cancellation() : cancellationToken);
    }

    internal CancellationToken Token(CancellationToken cancellationToken)
    {
        return cancellationToken == default ? _cancellation() : cancellationToken;
    }

    internal void NotifyVerified() => _verified();
}

/// <summary>A query that resolves against a fresh observation every time it is used.</summary>
public sealed class Locator
{
    private readonly Screen _screen;

    internal Locator(Screen screen, LocatorQuery query)
    {
        _screen = screen;
        Query = query;
    }

    internal LocatorQuery Query { get; }

    public Locator Filter(string hasText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hasText);
        return new Locator(_screen, Query.With(hasText: hasText));
    }

    public Locator First() => Nth(0);

    public Locator Nth(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return new Locator(_screen, Query.With(index: index));
    }

    public Locator GetByRole(string role, string? name = null, bool exact = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return new Locator(_screen, new LocatorQuery
        {
            Parent = Query,
            Kind = "role",
            Role = role,
            Name = name,
            Exact = exact,
        });
    }

    public Task TapAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.Tap(), options, cancellationToken);
    }

    /// <summary>Alias of <see cref="TapAsync"/>.</summary>
    public Task ClickAsync(ActionOptions? options = null, CancellationToken cancellationToken = default) => TapAsync(options, cancellationToken);

    public Task DoubleTapAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.DoubleTap(), options, cancellationToken);
    }

    public Task FillAsync(string value, ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ActAsync(new LocatorAction.Fill(value), options, cancellationToken);
    }

    /// <summary>Fills a secret. The engine marks the value sensitive and never reports it.</summary>
    public Task FillAsync(Secret value, ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ActAsync(new LocatorAction.Fill(value.Value, Sensitive: true), options, cancellationToken);
    }

    /// <summary>
    /// Focuses the input and types <paramref name="text"/> one character at a time,
    /// so the app receives key events. <see cref="PressSequentiallyOptions.Delay"/> waits between characters.
    /// </summary>
    public Task PressSequentiallyAsync(string text, PressSequentiallyOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (options?.Delay is TimeSpan wait)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(wait, TimeSpan.Zero, nameof(options));
        }

        return ActAsync(new LocatorAction.PressSequentially(text, options?.Delay), options, cancellationToken);
    }

    public Task PressAsync(string key, ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return ActAsync(new LocatorAction.Press(key), options, cancellationToken);
    }

    public Task CheckAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.Check(), options, cancellationToken);
    }

    public Task UncheckAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.Uncheck(), options, cancellationToken);
    }

    public Task ClearAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.Clear(), options, cancellationToken);
    }

    /// <summary>Selects the option whose value or label is <paramref name="value"/>.</summary>
    public Task SelectOptionAsync(string value, ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ActAsync(new LocatorAction.Select(value), options, cancellationToken);
    }

    public Task FocusAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.Focus(), options, cancellationToken);
    }

    public Task ScrollIntoViewAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.ScrollIntoView(), options, cancellationToken);
    }

    /// <summary>Reads text once. Does not retry and does not verify an earlier <c>act</c>.</summary>
    public async Task<string?> TextContentAsync(CancellationToken cancellationToken = default)
    {
        var node = await ResolveStrictAsync(cancellationToken).ConfigureAwait(false);
        return node.Text ?? node.Name;
    }

    /// <summary>Reads an input value once. Does not retry and does not verify an earlier <c>act</c>.</summary>
    public async Task<string?> InputValueAsync(CancellationToken cancellationToken = default)
    {
        var node = await ResolveStrictAsync(cancellationToken).ConfigureAwait(false);
        return node.Value;
    }

    /// <summary>Reads one platform attribute of exactly one match once, or null when the node does not have it.</summary>
    public async Task<string?> GetAttributeAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var node = await ResolveStrictAsync(cancellationToken).ConfigureAwait(false);
        return node.Attributes is not null && node.Attributes.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Reads current visibility: true when exactly one visible node matches. More than one match fails.</summary>
    public async Task<bool> IsVisibleAsync(CancellationToken cancellationToken = default)
    {
        return await ResolveSingleAsync(_screen.Token(cancellationToken)).ConfigureAwait(false) is not null;
    }

    /// <summary>Reads current hidden or absent state, the negation of <see cref="IsVisibleAsync"/>.</summary>
    public async Task<bool> IsHiddenAsync(CancellationToken cancellationToken = default)
    {
        return !await IsVisibleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the enabled state of exactly one match once.</summary>
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var node = await ResolveStrictAsync(cancellationToken).ConfigureAwait(false);
        return !node.States.Disabled;
    }

    /// <summary>Reads the disabled state of exactly one match once, the negation of <see cref="IsEnabledAsync"/>.</summary>
    public async Task<bool> IsDisabledAsync(CancellationToken cancellationToken = default)
    {
        return !await IsEnabledAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the checked state of exactly one match once.</summary>
    public async Task<bool> IsCheckedAsync(CancellationToken cancellationToken = default)
    {
        var node = await ResolveStrictAsync(cancellationToken).ConfigureAwait(false);
        return node.States.Checked;
    }

    /// <summary>Reads the viewport-relative box of exactly one match once, or null when the engine does not report one.</summary>
    public async Task<BoundingBox?> BoundingBoxAsync(CancellationToken cancellationToken = default)
    {
        var node = await ResolveStrictAsync(cancellationToken).ConfigureAwait(false);
        return node.Rect;
    }

    /// <summary>Counts current matches without waiting.</summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var matches = await ResolveAsync(_screen.Token(cancellationToken)).ConfigureAwait(false);
        return matches.Count;
    }

    /// <summary>One <see cref="Nth"/> locator per current match, without waiting. Empty when nothing matches.</summary>
    public async Task<IReadOnlyList<Locator>> AllAsync(CancellationToken cancellationToken = default)
    {
        var count = await CountAsync(cancellationToken).ConfigureAwait(false);
        var locators = new List<Locator>(count);
        for (var i = 0; i < count; i++)
        {
            locators.Add(Nth(i));
        }

        return locators;
    }

    /// <summary>Reads the text of every current match, without waiting. Empty when nothing matches.</summary>
    public async Task<IReadOnlyList<string>> AllTextContentsAsync(CancellationToken cancellationToken = default)
    {
        var matches = await ResolveAsync(_screen.Token(cancellationToken)).ConfigureAwait(false);
        return matches.Select(node => node.Text ?? node.Name ?? "").ToList();
    }

    /// <summary>
    /// Waits for <see cref="LocatorWaitForOptions.State"/>, <see cref="WaitForState.Visible"/> by default:
    /// <see cref="WaitForState.Attached"/> for one match, visible or not;
    /// <see cref="WaitForState.Detached"/> for none; <see cref="WaitForState.Hidden"/> for none
    /// or a hidden one. The timeout defaults to the action timeout. More than one match fails at once.
    /// Does not verify an earlier <c>act</c>.
    /// </summary>
    public async Task WaitForAsync(LocatorWaitForOptions? options = null, CancellationToken cancellationToken = default)
    {
        var state = options?.State ?? WaitForState.Visible;
        var token = _screen.Token(cancellationToken);
        var deadline = DateTime.UtcNow + TimeoutOf(options?.Timeout);
        var includeHidden = state is WaitForState.Attached or WaitForState.Detached;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var observation = await _screen.ObserveAsync(token).ConfigureAwait(false);
            var matches = LocatorResolver.Resolve(observation, Query, includeHidden);
            if (matches.Count > 1 && state is not WaitForState.Detached)
            {
                throw Ambiguous(matches.Count);
            }

            var done = state switch
            {
                WaitForState.Attached or WaitForState.Visible => matches.Count == 1,
                _ => matches.Count == 0,
            };
            if (done)
            {
                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TestException(
                    "TIMEOUT",
                    Query.Describe() + ".waitFor(" + StateName(state) + ") timed out: matched " + matches.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " nodes.");
            }

            await Task.Delay(_screen.PollInterval, token).ConfigureAwait(false);
        }
    }

    internal async Task<IReadOnlyList<SemanticNode>> ResolveAsync(CancellationToken cancellationToken)
    {
        var observation = await _screen.ObserveAsync(cancellationToken).ConfigureAwait(false);
        return LocatorResolver.Resolve(observation, Query);
    }

    internal Screen Screen => _screen;

    /// <summary>
    /// Waits up to the action timeout for exactly one enabled match, then acts on it.
    /// More than one match fails at once.
    /// </summary>
    private async Task ActAsync(LocatorAction action, ActionOptions? options, CancellationToken cancellationToken)
    {
        var token = _screen.Token(cancellationToken);
        var deadline = DateTime.UtcNow + TimeoutOf(options?.Timeout);
        while (true)
        {
            var node = await ResolveSingleAsync(token).ConfigureAwait(false);
            if (node is { States.Disabled: false })
            {
                await _screen.PerformAsync(node, action, token).ConfigureAwait(false);
                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw node is null
                    ? NotFound()
                    : new TestException("NOT_ACTIONABLE", Query.Describe() + " is disabled.");
            }

            await Task.Delay(_screen.PollInterval, token).ConfigureAwait(false);
        }
    }

    private async Task<SemanticNode> ResolveStrictAsync(CancellationToken cancellationToken)
    {
        var node = await ResolveSingleAsync(_screen.Token(cancellationToken)).ConfigureAwait(false);
        return node ?? throw NotFound();
    }

    /// <summary>Returns the one match, or null when nothing matches. More than one match fails.</summary>
    private async Task<SemanticNode?> ResolveSingleAsync(CancellationToken token)
    {
        var matches = await ResolveAsync(token).ConfigureAwait(false);
        if (matches.Count > 1)
        {
            throw Ambiguous(matches.Count);
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    private TimeSpan TimeoutOf(TimeSpan? timeout)
    {
        if (timeout is TimeSpan chosen)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(chosen, TimeSpan.Zero, "timeout");
            return chosen;
        }

        return _screen.ActionTimeout;
    }

    private static string StateName(WaitForState state) => state switch
    {
        WaitForState.Attached => "attached",
        WaitForState.Detached => "detached",
        WaitForState.Hidden => "hidden",
        _ => "visible",
    };

    private TestException Ambiguous(int count) => new("STRICT_MODE", Query.Describe() + " matched " + count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " nodes.");

    private TestException NotFound() => new("NOT_FOUND", Query.Describe() + " matched 0 nodes.");
}

/// <summary>Options for one locator action.</summary>
public class ActionOptions
{
    /// <summary>How long to wait for exactly one enabled match. Defaults to the configured action timeout.</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>Options for <see cref="Locator.PressSequentiallyAsync"/>.</summary>
public sealed class PressSequentiallyOptions : ActionOptions
{
    /// <summary>How long to wait between characters.</summary>
    public TimeSpan? Delay { get; init; }
}

/// <summary>The state <see cref="Locator.WaitForAsync"/> waits for.</summary>
public enum WaitForState
{
    /// <summary>Exactly one visible match.</summary>
    Visible,

    /// <summary>Exactly one match, visible or not.</summary>
    Attached,

    /// <summary>No match, visible or not.</summary>
    Detached,

    /// <summary>No visible match.</summary>
    Hidden,
}

/// <summary>Options for <see cref="Locator.WaitForAsync"/>.</summary>
public sealed class LocatorWaitForOptions
{
    public WaitForState State { get; init; } = WaitForState.Visible;

    /// <summary>How long to wait. Defaults to the configured action timeout.</summary>
    public TimeSpan? Timeout { get; init; }
}

internal sealed class LocatorQuery
{
    public LocatorQuery? Parent { get; init; }

    public string Kind { get; init; } = "role";

    public string? Role { get; init; }

    public string? Name { get; init; }

    public bool Exact { get; init; } = true;

    public string? TestId { get; init; }

    public string? Text { get; init; }

    public string? HasText { get; init; }

    public int? Index { get; init; }

    public LocatorQuery With(string? hasText = null, int? index = null)
    {
        return new LocatorQuery
        {
            Parent = Parent,
            Kind = Kind,
            Role = Role,
            Name = Name,
            Exact = Exact,
            TestId = TestId,
            Text = Text,
            HasText = hasText ?? HasText,
            Index = index ?? Index,
        };
    }

    public string Describe()
    {
        var core = Kind switch
        {
            "role" => Name is null ? "getByRole(\"" + Role + "\")" : "getByRole(\"" + Role + "\", \"" + Name + "\")",
            "text" => "getByText(\"" + Text + "\")",
            "label" => "getByLabel(\"" + Name + "\")",
            "testid" => "getByTestId(\"" + TestId + "\")",
            "placeholder" => "getByPlaceholder(\"" + Name + "\")",
            _ => Kind,
        };
        if (HasText is not null)
        {
            core += ".filter(hasText: \"" + HasText + "\")";
        }

        if (Index is int nth)
        {
            core += ".nth(" + nth.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        }

        return core;
    }
}

internal static class LocatorResolver
{
    private static readonly HashSet<string> LabelRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "textbox",
        "checkbox",
        "radio",
        "combobox",
        "searchbox",
    };

    /// <summary>
    /// Returns the nodes <paramref name="query"/> names. Hidden nodes never match,
    /// unless <paramref name="includeHidden"/> asks for every attached node.
    /// </summary>
    public static IReadOnlyList<SemanticNode> Resolve(Observation observation, LocatorQuery query, bool includeHidden = false)
    {
        var matches = new List<SemanticNode>();
        if (query.Parent is null)
        {
            foreach (var root in observation.Roots)
            {
                Collect(root, query, matches, includeHidden);
            }
        }
        else
        {
            foreach (var parent in Resolve(observation, query.Parent, includeHidden))
            {
                foreach (var child in parent.Children)
                {
                    Collect(child, query, matches, includeHidden);
                }
            }
        }

        if (query.Kind is "text" or "label")
        {
            matches = InnermostOnly(matches);
        }

        if (query.HasText is not null)
        {
            matches = matches.Where(node => ContainsText(node, query.HasText)).ToList();
        }

        if (query.Index is int index)
        {
            if (index < 0 || index >= matches.Count)
            {
                return [];
            }

            return [matches[index]];
        }

        return matches;
    }

    public static IEnumerable<SemanticNode> Walk(IEnumerable<SemanticNode> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in Walk(root.Children))
            {
                yield return child;
            }
        }
    }

    private static void Collect(SemanticNode node, LocatorQuery query, List<SemanticNode> matches, bool includeHidden)
    {
        if ((includeHidden || !node.States.Hidden) && Matches(node, query))
        {
            matches.Add(node);
        }

        foreach (var child in node.Children)
        {
            Collect(child, query, matches, includeHidden);
        }
    }

    private static bool Matches(SemanticNode node, LocatorQuery query)
    {
        return query.Kind switch
        {
            "role" => RoleIs(node, query.Role) && (query.Name is null || TextRules.Matches(node.Name, query.Name, query.Exact)),
            "text" => TextRules.Matches(node.Text, query.Text ?? "", query.Exact) || TextRules.Matches(node.Name, query.Text ?? "", query.Exact),
            "label" => node.Role is not null && LabelRoles.Contains(node.Role) && TextRules.Matches(node.Name, query.Name ?? "", query.Exact),
            "testid" => string.Equals(node.TestId, query.TestId, StringComparison.Ordinal),
            "placeholder" => TextRules.Matches(node.Placeholder, query.Name ?? "", query.Exact),
            _ => false,
        };
    }

    /// <summary>
    /// Drops every match that contains another match. A container that repeats
    /// its child's text is not a second match, as upstream answers text and
    /// label queries with the innermost node.
    /// </summary>
    private static List<SemanticNode> InnermostOnly(List<SemanticNode> matches)
    {
        var set = new HashSet<SemanticNode>(matches);
        return matches.Where(node => !Walk(node.Children).Any(set.Contains)).ToList();
    }

    private static bool RoleIs(SemanticNode node, string? role)
    {
        return node.Role is not null && role is not null && string.Equals(node.Role, role, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsText(SemanticNode node, string expected)
    {
        if (TextRules.Matches(node.Name, expected, exact: false) || TextRules.Matches(node.Text, expected, exact: false) || TextRules.Matches(node.Value, expected, exact: false))
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (ContainsText(child, expected))
            {
                return true;
            }
        }

        return false;
    }
}
