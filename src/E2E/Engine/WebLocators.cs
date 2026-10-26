// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// What the web engine reads off a <see cref="LocatorExpression"/> before it resolves it (upstream's <c>web/src/locators.ts</c>). Upstream
/// projects an expression onto a Playwright locator there. The port's web engine resolves over its semantic tree, so only the
/// <c>selector</c> and <c>frame</c> kinds go to Playwright (see <c>WebSession.ProjectAsync</c>); what stays here is the part that reads
/// the expression itself.
/// </summary>
internal static class WebLocators
{
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
            case "selector" or "refs":
                return [];
            case "frame":
                return [expression.Selector!, .. FrameSelectors(expression.Source!)];
            default:
                return expression.Parent is null ? [] : FrameSelectors(expression.Parent);
        }
    }

    /// <summary>
    /// The expression with the frame scopes of its queries taken off, for a filter that resolves inside the matches of a source that has
    /// them. A <c>selector</c> keeps its frame: the page decides which elements it names.
    /// </summary>
    public static LocatorExpression WithoutFrameScopes(LocatorExpression expression)
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
                return expression with { Source = WithoutFrameScopes(expression.Source!), Has = expression.Has is null ? null : WithoutFrameScopes(expression.Has) };
            case "index":
                return expression with { Source = WithoutFrameScopes(expression.Source!) };
            default:
                return expression.Parent is null ? expression : expression with { Parent = WithoutFrameScopes(expression.Parent) };
        }
    }

    /// <summary>Whether resolving the expression needs the page: it holds a <c>selector</c> or a <c>frame</c> somewhere.</summary>
    public static bool NeedsPage(LocatorExpression expression) =>
        expression.Kind is "selector" or "frame"
        || (expression.Source is { } source && NeedsPage(source))
        || (expression.Parent is { } parent && NeedsPage(parent))
        || (expression.Left is { } left && NeedsPage(left))
        || (expression.Right is { } right && NeedsPage(right))
        || (expression.Has is { } has && NeedsPage(has));

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
