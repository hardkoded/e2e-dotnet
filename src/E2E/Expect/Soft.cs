// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace E2E;

/// <summary>
/// The locator matchers of <see cref="LocatorExpect"/>, but an
/// <c>ASSERTION_FAILED</c> is kept on the session instead of thrown, and the
/// test body runs on. Any other error, such as <c>LOCATOR_AMBIGUOUS</c>, still throws.
/// </summary>
public sealed class SoftLocatorExpect
{
    private readonly LocatorExpect _expect;
    private readonly SoftFailures _failures;

    internal SoftLocatorExpect(LocatorExpect expect, SoftFailures failures)
    {
        _expect = expect;
        _failures = failures;
    }

    /// <summary>Inverts the matcher, as <see cref="LocatorExpect.Not"/>. A failure is still kept, not thrown.</summary>
    public SoftLocatorExpect Not => new(_expect.Not, _failures);

    public Task ToBeVisibleAsync(bool visible = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToBeVisibleAsync(visible, timeout, cancellationToken));

    public Task ToBeHiddenAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToBeHiddenAsync(timeout, cancellationToken));

    public Task ToBeAttachedAsync(bool attached = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToBeAttachedAsync(attached, timeout, cancellationToken));

    public Task ToBeEnabledAsync(bool enabled = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToBeEnabledAsync(enabled, timeout, cancellationToken));

    public Task ToBeDisabledAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToBeDisabledAsync(timeout, cancellationToken));

    public Task ToBeCheckedAsync(bool isChecked = true, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToBeCheckedAsync(isChecked, timeout, cancellationToken));

    public Task ToBeSelectedAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToBeSelectedAsync(timeout, cancellationToken));

    public Task ToBeExpandedAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToBeExpandedAsync(timeout, cancellationToken));

    public Task ToBeFocusedAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToBeFocusedAsync(timeout, cancellationToken));

    public Task ToHaveTextAsync(TextMatch expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToHaveTextAsync(expected, ignoreCase, timeout, cancellationToken));

    public Task ToHaveTextAsync(IReadOnlyList<TextMatch> expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToHaveTextAsync(expected, ignoreCase, timeout, cancellationToken));

    public Task ToContainTextAsync(TextMatch expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToContainTextAsync(expected, ignoreCase, timeout, cancellationToken));

    public Task ToContainTextAsync(IReadOnlyList<TextMatch> expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToContainTextAsync(expected, ignoreCase, timeout, cancellationToken));

    public Task ToHaveValueAsync(TextMatch expected, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToHaveValueAsync(expected, timeout, cancellationToken));

    public Task ToHaveAccessibleNameAsync(TextMatch expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToHaveAccessibleNameAsync(expected, ignoreCase, timeout, cancellationToken));

    public Task ToHaveAttributeAsync(string name, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToHaveAttributeAsync(name, timeout, cancellationToken));

    public Task ToHaveAttributeAsync(string name, TextMatch value, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToHaveAttributeAsync(name, value, ignoreCase, timeout, cancellationToken));

    public Task ToHaveScreenshotAsync(string? name = null, ScreenshotOptions? options = null, [CallerFilePath] string? callerFile = null) => KeepAsync(_expect.ToHaveScreenshotAsync(name, options, callerFile));

    public Task ToHaveScreenshotAsync(ScreenshotOptions options, [CallerFilePath] string? callerFile = null) => KeepAsync(_expect.ToHaveScreenshotAsync(options, callerFile));

    public Task ToHaveCountAsync(int count, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => KeepAsync(_expect.ToHaveCountAsync(count, timeout, cancellationToken));

    private Task KeepAsync(Task matcher) => _failures.KeepAsync(matcher);
}

/// <summary>
/// The screen matchers of <see cref="ScreenExpect"/>, but an
/// <c>ASSERTION_FAILED</c> is kept on the session instead of thrown, as <see cref="SoftLocatorExpect"/> does.
/// </summary>
public sealed class SoftScreenExpect
{
    private readonly ScreenExpect _expect;
    private readonly SoftFailures _failures;

    internal SoftScreenExpect(ScreenExpect expect, SoftFailures failures)
    {
        _expect = expect;
        _failures = failures;
    }

    /// <summary>Inverts the matcher, as <see cref="ScreenExpect.Not"/>. A failure is still kept, not thrown.</summary>
    public SoftScreenExpect Not => new(_expect.Not, _failures);

    public Task ToHaveScreenshotAsync(string? name = null, ScreenshotOptions? options = null, [CallerFilePath] string? callerFile = null) => _failures.KeepAsync(_expect.ToHaveScreenshotAsync(name, options, callerFile));

    public Task ToHaveScreenshotAsync(ScreenshotOptions options, [CallerFilePath] string? callerFile = null) => _failures.KeepAsync(_expect.ToHaveScreenshotAsync(options, callerFile));
}

/// <summary>
/// The browser matchers of <see cref="BrowserExpect"/>, but an
/// <c>ASSERTION_FAILED</c> is kept on the session instead of thrown, as <see cref="SoftLocatorExpect"/> does.
/// </summary>
public sealed class SoftBrowserExpect
{
    private readonly BrowserExpect _expect;
    private readonly SoftFailures _failures;

    internal SoftBrowserExpect(BrowserExpect expect, SoftFailures failures)
    {
        _expect = expect;
        _failures = failures;
    }

    /// <summary>Inverts the matcher, as <see cref="BrowserExpect.Not"/>. A failure is still kept, not thrown.</summary>
    public SoftBrowserExpect Not => new(_expect.Not, _failures);

    public Task ToHaveURLAsync(string expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => _failures.KeepAsync(_expect.ToHaveURLAsync(expected, ignoreCase, timeout, cancellationToken));

    public Task ToHaveURLAsync(Regex expected, bool? ignoreCase = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => _failures.KeepAsync(_expect.ToHaveURLAsync(expected, ignoreCase, timeout, cancellationToken));

    public Task ToHaveTitleAsync(TextMatch expected, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => _failures.KeepAsync(_expect.ToHaveTitleAsync(expected, timeout, cancellationToken));

    public Task ToHaveClassAsync(Locator target, TextMatch expected, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => _failures.KeepAsync(_expect.ToHaveClassAsync(target, expected, timeout, cancellationToken));
}

/// <summary>
/// The soft failures of one session. Each kept failure goes to the handler
/// when there is one, otherwise it waits for <see cref="Close"/>. Once closed,
/// nothing is kept and a soft matcher throws as a hard one would.
/// </summary>
internal sealed class SoftFailures
{
    private readonly List<TestException> _failures = [];
    private readonly Action<TestException>? _handler;
    private readonly Lock _gate = new();
    private bool _open = true;
    private bool _kept;

    public SoftFailures(Action<TestException>? handler = null)
    {
        _handler = handler;
    }

    /// <summary>Replaces the secrets the attempt knows when the failures are reported; the text as it is outside a session.</summary>
    internal Func<string, string> Redact { get; set; } = static text => text;

    /// <summary>Whether any failure was kept, whether it waits for <see cref="Close"/> or went to the handler.</summary>
    public bool Any
    {
        get
        {
            lock (_gate)
            {
                return _kept;
            }
        }
    }

    /// <summary>Awaits <paramref name="matcher"/> and keeps its <c>ASSERTION_FAILED</c>. Any other error still throws.</summary>
    public async Task KeepAsync(Task matcher)
    {
        try
        {
            await matcher.ConfigureAwait(false);
        }
        catch (TestException ex) when (ex.Code == "ASSERTION_FAILED" && Keep(ex))
        {
        }
    }

    public bool Keep(TestException error)
    {
        lock (_gate)
        {
            if (!_open)
            {
                return false;
            }

            _kept = true;
            if (_handler is null)
            {
                _failures.Add(error);
                return true;
            }
        }

        _handler(error);
        return true;
    }

    /// <summary>Ends collection and returns one <c>ASSERTION_FAILED</c> for everything kept, or null.</summary>
    public TestException? Close()
    {
        TestException[] failures;
        lock (_gate)
        {
            _open = false;
            failures = [.. _failures];
            _failures.Clear();
        }

        if (failures.Length == 0)
        {
            return null;
        }

        var lines = new List<string>
        {
            failures.Length.ToString(CultureInfo.InvariantCulture) + " soft assertion" + (failures.Length == 1 ? "" : "s") + " failed",
        };
        for (var i = 0; i < failures.Length; i++)
        {
            lines.Add((i + 1).ToString(CultureInfo.InvariantCulture) + ". " + failures[i].Message.Replace("\n", "\n   ", StringComparison.Ordinal));
        }

        return ErrorRedaction.Failure("ASSERTION_FAILED", string.Join('\n', lines), failures[0], Redact);
    }
}

/// <summary>
/// The value matchers of <see cref="ValueExpectation{T}"/>, but an <c>ASSERTION_FAILED</c> is kept on the session instead of thrown, and the
/// test body runs on. Any other error, such as <c>INVALID_ARGUMENT</c>, still throws.
/// </summary>
public sealed class SoftValueExpectation<T>
{
    private readonly ValueExpectation<T> _expect;
    private readonly SoftFailures _failures;

    internal SoftValueExpectation(ValueExpectation<T> expect, SoftFailures failures)
    {
        _expect = expect;
        _failures = failures;
    }

    /// <summary>Inverts the matcher, as <see cref="ValueExpectation{T}.Not"/>. A failure is still kept, not thrown.</summary>
    public SoftValueExpectation<T> Not => new(_expect.Not, _failures);

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToBe(T expected) => Keep(() => _expect.ToBe(expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToEqual(object? expected) => Keep(() => _expect.ToEqual(expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToMatchObject(object expected) => Keep(() => _expect.ToMatchObject(expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToBeTruthy() => Keep(_expect.ToBeTruthy);

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToBeFalsy() => Keep(_expect.ToBeFalsy);

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToBeNull() => Keep(_expect.ToBeNull);

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToBeUndefined() => Keep(_expect.ToBeUndefined);

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToBeDefined() => Keep(_expect.ToBeDefined);

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToHaveLength(int expected) => Keep(() => _expect.ToHaveLength(expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToHaveProperty(string path) => Keep(() => _expect.ToHaveProperty(path));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToHaveProperty(string path, object? expected) => Keep(() => _expect.ToHaveProperty(path, expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToHaveProperty(IReadOnlyList<object> path) => Keep(() => _expect.ToHaveProperty(path));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToHaveProperty(IReadOnlyList<object> path, object? expected) => Keep(() => _expect.ToHaveProperty(path, expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToContain(object? expected) => Keep(() => _expect.ToContain(expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToMatch(string expected) => Keep(() => _expect.ToMatch(expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToMatch(Regex expected) => Keep(() => _expect.ToMatch(expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToBeGreaterThan(double expected) => Keep(() => _expect.ToBeGreaterThan(expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToBeGreaterThanOrEqual(double expected) => Keep(() => _expect.ToBeGreaterThanOrEqual(expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToBeLessThan(double expected) => Keep(() => _expect.ToBeLessThan(expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToBeLessThanOrEqual(double expected) => Keep(() => _expect.ToBeLessThanOrEqual(expected));

    /// <summary>The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept.</summary>
    public void ToBeCloseTo(double expected, int digits = 2) => Keep(() => _expect.ToBeCloseTo(expected, digits));

    /// <summary>
    /// The matcher of the same name on <see cref="ValueExpectation{T}"/>, with a failure kept. It returns the schema's output when the value
    /// matches, and <c>null</c> (the default) after a kept failure, as it does when negated.
    /// </summary>
    public TOutput? ToMatchSchema<TOutput>(StandardSchemaV1<TOutput> schema)
    {
        TOutput? output = default;
        Keep(() => output = _expect.ToMatchSchema(schema));
        return output;
    }

    private void Keep(Action matcher)
    {
        try
        {
            matcher();
        }
        catch (TestException ex) when (ex.Code == "ASSERTION_FAILED" && _failures.Keep(ex))
        {
        }
    }
}
