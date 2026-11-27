// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop.MacOs;
using E2E.Engine;

namespace E2E.Desktop;

/// <summary>A failure the driver reported, before translation, for a caller that tries another way when nothing was sent. Port of upstream's <c>DriverFailure</c>.</summary>
public sealed class DriverFailure : Exception
{
    public DriverFailure(DriverError failure)
        : base(failure?.Message)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Code = failure.Code;
        NotDelivered = failure.Disposition?.Delivery == "not_delivered";
        Details = failure.Details ?? new Dictionary<string, object?>();
    }

    public string Code { get; }

    /// <summary>Whether the driver says nothing reached the app, so another way may be tried.</summary>
    public bool NotDelivered { get; }

    public IReadOnlyDictionary<string, object?> Details { get; }
}

/// <summary>One driver call to bound and translate. Port of upstream's <c>DriverCall</c>.</summary>
internal sealed record DriverCall
{
    /// <summary>Whether the call sends input to an app; decides how a cut-off or failed call is reported.</summary>
    public required bool Input { get; init; }

    public CancellationToken Signal { get; init; }

    /// <summary>The call is given up once this budget is spent, in milliseconds.</summary>
    public required int TimeoutMs { get; init; }

    /// <summary>Names the call in an error message.</summary>
    public required string Label { get; init; }

    /// <summary>Runs when the budget or the signal cuts the call off, to stop work the driver would otherwise go on with.</summary>
    public Action? OnCutOff { get; init; }
}

/// <summary>
/// .NET-only holder name (C# has no free functions): upstream's <c>call</c>, <c>translate</c>, and <c>loadNativeDriver</c> in <c>driver.ts</c>. The
/// native side is <see cref="MacOsDriver"/>, which upstream loads from the <c>e2e-desktop-napi</c> addon.
/// <list type="table">
/// <listheader><term>Driver result</term><description>Code</description></listheader>
/// <item><term><c>STALE_REF</c>, <c>ELEMENT_NOT_FOUND</c>, sent nothing</term><description>NODE_STALE (retryable)</description></item>
/// <item><term><c>APP_UNRESPONSIVE</c> that <c>details.retryable</c> says is safe to retry</term><description>NODE_STALE (retryable)</description></item>
/// <item><term><c>ACTION_NOT_SUPPORTED</c>, <c>POLICY_DENIED</c></term><description>UNSUPPORTED_CAPABILITY</description></item>
/// <item><term><c>APP_NOT_FOUND</c>, <c>WINDOW_NOT_FOUND</c></term><description>INVALID_STATE</description></item>
/// <item><term><c>ACTION_FAILED</c>, <c>TIMEOUT</c>, sent nothing</term><description>NOT_ACTIONABLE</description></item>
/// <item><term><c>TIMEOUT</c> of a read</term><description>OPERATION_TIMEOUT</description></item>
/// <item><term>any failure of an input that may have been delivered</term><description>ACTION_MAY_HAVE_COMMITTED</description></item>
/// <item><term>an input still running when the operation's budget ran out</term><description>ACTION_MAY_HAVE_COMMITTED</description></item>
/// <item><term>a read still running when the operation's budget ran out</term><description>OPERATION_TIMEOUT</description></item>
/// <item><term>the operation's signal aborted</term><description>CANCELLED</description></item>
/// <item><term>anything else, <c>PERM_DENIED</c> included</term><description>ENGINE_FAILURE</description></item>
/// </list>
/// The driver states delivery on every failure (<c>disposition.delivery</c>), so the line between "refused" and "may have committed" is its own,
/// never a guess from the error code.
/// </summary>
internal static class DesktopDriverCalls
{
    /// <summary>The least time an input gets once started, however little budget was left: a click that lands is then not reported as cut off. A read gets exactly its budget.</summary>
    private const int MinInputMs = 500;

    private static readonly HashSet<string> StaleCodes = ["STALE_REF", "ELEMENT_NOT_FOUND"];
    private static readonly HashSet<string> UnsupportedCodes = ["ACTION_NOT_SUPPORTED", "POLICY_DENIED"];
    private static readonly HashSet<string> GoneCodes = ["APP_NOT_FOUND", "WINDOW_NOT_FOUND"];
    private static readonly HashSet<string> RefusedCodes = ["ACTION_FAILED", "TIMEOUT"];

    /// <summary>
    /// Loads the driver built for this machine. Throws <c>UNSUPPORTED_CAPABILITY</c> on a platform no driver is built for. Upstream's
    /// <c>loadNativeDriver</c>, whose addon the macOS builds ship: the port's driver is managed code over the system frameworks.
    /// </summary>
    public static IDesktopDriver LoadNativeDriver() =>
        LoadNativeDriver(OperatingSystem.IsMacOS(), DesktopOptionsResolver.ProcessPlatform());

    /// <summary><see cref="LoadNativeDriver()"/> for a host the caller has already asked (.NET-only: tests answer for either OS).</summary>
    internal static IDesktopDriver LoadNativeDriver(bool isMacOS, string os)
    {
        if (!isMacOS)
        {
            throw new EngineException(
                "UNSUPPORTED_CAPABILITY",
                $"E2E.Desktop has no native driver for {os} on {DesktopOptionsResolver.ProcessArch()}: it drives macOS apps only",
                retryable: false
            );
        }

        return new MacOsDriver();
    }

    /// <summary>
    /// Runs one driver call and returns its <c>data</c>. A failure the envelope reports is thrown as <see cref="DriverFailure"/>, untranslated,
    /// so a caller can try another way when nothing was sent; <see cref="Translate"/> turns it into the engine contract. A call cut off by the
    /// budget or the signal keeps running, and its answer is dropped: an input may still land, which is why it is reported as one that may have
    /// committed. <see cref="DriverCall.OnCutOff"/> then stops what the driver can stop, such as any input an action had yet to send. Upstream's <c>call</c>.
    /// </summary>
    public static async Task<T> CallAsync<T>(Func<Task<Envelope<T>>> start, DriverCall options)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Signal.IsCancellationRequested)
        {
            throw new EngineException("CANCELLED", $"{options.Label} was cancelled", retryable: false);
        }

        using var timer = new CancellationTokenSource();
        var limit = options.Input ? Math.Max(options.TimeoutMs, MinInputMs) : options.TimeoutMs;
        var timeout = Task.Delay(Math.Max(limit, 0), timer.Token);
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = options.Signal.Register(() => aborted.TrySetResult());
        Task<Envelope<T>> running;
        try
        {
            running = start();
        }
        catch (Exception cause)
        {
            throw new EngineException(
                "ENGINE_FAILURE",
                $"{options.Label}: the native driver failed: {cause.Message}",
                retryable: false,
                cause
            );
        }

        Task winner;
        try
        {
            winner = await Task.WhenAny(running, timeout, aborted.Task).ConfigureAwait(false);
        }
        finally
        {
            await timer.CancelAsync().ConfigureAwait(false);
        }

        if (winner != running)
        {
            // The answer is dropped, but a failure of the abandoned call is observed so it never goes unobserved.
            _ = running.ContinueWith(static task => _ = task.Exception, TaskScheduler.Default);
            options.OnCutOff?.Invoke();
            if (winner == aborted.Task)
            {
                throw new EngineException("CANCELLED", $"{options.Label} was cancelled", retryable: false);
            }

            throw options.Input
                ? new EngineException(
                    "ACTION_MAY_HAVE_COMMITTED",
                    $"{options.Label} timed out before the driver answered; its input may have reached the app",
                    retryable: false
                )
                : new EngineException("OPERATION_TIMEOUT", $"{options.Label} timed out", retryable: false);
        }

        Envelope<T> outcome;
        try
        {
            outcome = await running.ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw new EngineException(
                "ENGINE_FAILURE",
                $"{options.Label}: the native driver failed: {cause.Message}",
                retryable: false,
                cause
            );
        }

        if (!outcome.Ok)
        {
            throw new DriverFailure(outcome.Error!);
        }

        return outcome.Data!;
    }

    /// <summary>
    /// The engine error for a failure of <paramref name="label"/>. An <see cref="EngineException"/> passes through. <paramref name="input"/> says
    /// whether the call sent input to the app: its failure is <c>ACTION_MAY_HAVE_COMMITTED</c> unless the driver says nothing was sent.
    /// Upstream's <c>translate</c>.
    /// </summary>
    public static Exception Translate(object? cause, string label, bool input)
    {
        if (cause is not DriverFailure failure)
        {
            // An exception passes through; anything else is an engine failure that names it, as upstream's `translate` does for a thrown non-Error.
            return cause as Exception ?? new EngineException("ENGINE_FAILURE", $"{label}: {cause}", retryable: false);
        }

        var text = $"{label}: {failure.Message}";
        if (input && !failure.NotDelivered)
        {
            return new EngineException(
                "ACTION_MAY_HAVE_COMMITTED",
                $"{text} (the input may have reached the app)",
                retryable: false,
                failure
            );
        }

        if (StaleCodes.Contains(failure.Code))
        {
            return new EngineException("NODE_STALE", text, retryable: true, failure);
        }

        // A read the driver says is safe to try again (the app was busy): read again, as a stale one.
        if (failure.Code == "APP_UNRESPONSIVE" && failure.Details.GetValueOrDefault("retryable") is true)
        {
            return new EngineException("NODE_STALE", text, retryable: true, failure);
        }

        if (UnsupportedCodes.Contains(failure.Code))
        {
            return new EngineException("UNSUPPORTED_CAPABILITY", text, retryable: false, failure);
        }

        if (GoneCodes.Contains(failure.Code))
        {
            return new EngineException(
                "INVALID_STATE",
                $"{text}; open the app with app.open()",
                retryable: false,
                failure
            );
        }

        // A read that ran out of time is a timeout; only an input that could not be delivered in time is not actionable.
        if (failure.Code == "TIMEOUT" && !input)
        {
            return new EngineException("OPERATION_TIMEOUT", text, retryable: false, failure);
        }

        if (RefusedCodes.Contains(failure.Code))
        {
            return new EngineException("NOT_ACTIONABLE", text, retryable: false, failure);
        }

        if (failure.Code == "PERM_DENIED")
        {
            return new EngineException(
                "ENGINE_FAILURE",
                $"{text}. Grant Accessibility (and Screen Recording, for screenshots) to the app that starts e2e, such as your terminal, then run again",
                retryable: false,
                failure
            );
        }

        return new EngineException("ENGINE_FAILURE", text, retryable: false, failure);
    }
}
