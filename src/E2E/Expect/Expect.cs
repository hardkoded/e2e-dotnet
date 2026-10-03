// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;

namespace E2E;

/// <summary>
/// Polling assertions for a <see cref="Locator"/>, <c>expect.soft</c>, and
/// <c>expect.poll</c>. A passing locator assertion verifies the previous
/// <c>agent.act</c>. Plain value matchers are not ported: use NUnit
/// <c>Assert.That</c>, and <c>Assert.EnterMultipleScope</c> for soft value checks.
/// </summary>
public static class Expect
{
    public static LocatorExpect That(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        return new LocatorExpect(locator);
    }

    /// <summary>
    /// The locator matchers, but a failure is kept on the session instead of
    /// thrown and the body runs on. The test fails at the end with every kept
    /// failure. <c>E2ETest</c> records each one as an NUnit assertion failure.
    /// </summary>
    public static SoftLocatorExpect Soft(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        return new SoftLocatorExpect(new LocatorExpect(locator), locator.Screen.SoftFailures);
    }

    /// <summary>Re-reads <paramref name="read"/> until the chosen matcher holds or the timeout passes.</summary>
    public static PollExpectation<T> Poll<T>(Func<CancellationToken, Task<T>> read, PollOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        options ??= new PollOptions();
        PollExpectation<T>.Validate(options);
        return new PollExpectation<T>(read, options, negated: false);
    }

    /// <inheritdoc cref="Poll{T}(Func{CancellationToken, Task{T}}, PollOptions?)"/>
    public static PollExpectation<T> Poll<T>(Func<Task<T>> read, PollOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        return Poll(_ => read(), options);
    }

    /// <inheritdoc cref="Poll{T}(Func{CancellationToken, Task{T}}, PollOptions?)"/>
    public static PollExpectation<T> Poll<T>(Func<T> read, PollOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        return Poll(_ => Task.FromResult(read()), options);
    }
}

/// <summary>Matchers that retry until they pass or the assertion timeout elapses.</summary>
public sealed class LocatorExpect
{
    private readonly Locator _locator;

    internal LocatorExpect(Locator locator)
    {
        _locator = locator;
    }

    public Task ToBeVisibleAsync(CancellationToken cancellationToken = default)
    {
        return PollAsync(
            "toBeVisible",
            matches => matches.Count == 1 ? null : "matched " + matches.Count.ToString(CultureInfo.InvariantCulture) + " nodes",
            strict: true,
            cancellationToken);
    }

    public Task ToBeHiddenAsync(CancellationToken cancellationToken = default)
    {
        return PollAsync(
            "toBeHidden",
            matches => matches.Count == 0 ? null : "matched " + matches.Count.ToString(CultureInfo.InvariantCulture) + " nodes",
            strict: false,
            cancellationToken);
    }

    public Task ToHaveTextAsync(string expected, bool exact = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return PollSingleAsync(
            "toHaveText",
            node => TextEquals(ShownText(node), expected, exact) ? null : "had text " + Quote(ShownText(node)),
            cancellationToken);
    }

    public Task ToContainTextAsync(string expected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return PollSingleAsync(
            "toContainText",
            node => Internal.TextRules.Contains(ShownText(node), expected) ? null : "had text " + Quote(ShownText(node)),
            cancellationToken);
    }

    public Task ToHaveCountAsync(int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return PollAsync(
            "toHaveCount",
            matches => matches.Count == count ? null : "matched " + matches.Count.ToString(CultureInfo.InvariantCulture) + " nodes",
            strict: false,
            cancellationToken);
    }

    public Task ToBeEnabledAsync(CancellationToken cancellationToken = default)
    {
        return PollSingleAsync("toBeEnabled", node => node.States.Disabled ? "was disabled" : null, cancellationToken);
    }

    public Task ToBeDisabledAsync(CancellationToken cancellationToken = default)
    {
        return PollSingleAsync("toBeDisabled", node => node.States.Disabled ? null : "was enabled", cancellationToken);
    }

    public Task ToBeCheckedAsync(CancellationToken cancellationToken = default)
    {
        return PollSingleAsync("toBeChecked", node => node.States.Checked ? null : "was not checked", cancellationToken);
    }

    public Task ToHaveValueAsync(string expected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return PollSingleAsync(
            "toHaveValue",
            node => string.Equals(node.Value ?? "", expected, StringComparison.Ordinal) ? null : "had value " + Quote(node.Value),
            cancellationToken);
    }

    private Task PollSingleAsync(string matcher, Func<SemanticNode, string?> check, CancellationToken cancellationToken)
    {
        return PollAsync(
            matcher,
            matches =>
            {
                if (matches.Count != 1)
                {
                    return "matched " + matches.Count.ToString(CultureInfo.InvariantCulture) + " nodes";
                }

                return check(matches[0]);
            },
            strict: true,
            cancellationToken);
    }

    private async Task PollAsync(string matcher, Func<IReadOnlyList<SemanticNode>, string?> check, bool strict, CancellationToken cancellationToken)
    {
        var token = _locator.Screen.Token(cancellationToken);
        var timeout = _locator.Screen.AssertionTimeout;
        var deadline = DateTime.UtcNow + timeout;
        string? failure = null;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var matches = await _locator.ResolveAsync(token).ConfigureAwait(false);
            failure = check(matches);
            if (failure is null)
            {
                _locator.Screen.NotifyVerified();
                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                var code = strict && IsStrict(failure) ? "STRICT_MODE" : "ASSERTION_FAILED";
                throw new TestException(
                    code,
                    "expect(" + _locator.Query.Describe() + ")." + matcher + " failed: " + failure);
            }

            await Task.Delay(_locator.Screen.PollInterval, token).ConfigureAwait(false);
        }
    }

    private static string? ShownText(SemanticNode node) => node.Text ?? node.Name;

    private static bool TextEquals(string? actual, string expected, bool exact)
    {
        return Internal.TextRules.Matches(actual, expected, exact);
    }

    private static string Quote(string? value) => "\"" + (value ?? "") + "\"";

    private static bool IsStrict(string failure)
    {
        const string prefix = "matched ";
        if (!failure.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = failure[prefix.Length..];
        var space = rest.IndexOf(' ');
        if (space <= 0)
        {
            return false;
        }

        return int.TryParse(rest[..space], NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count > 1;
    }
}
