// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>The kind of a native dialog, independent of its message (upstream's <c>Dialog['type']</c>).</summary>
public enum DialogType
{
    /// <summary><c>alert</c>.</summary>
    Alert,

    /// <summary><c>confirm</c>.</summary>
    Confirm,

    /// <summary><c>prompt</c>.</summary>
    Prompt,

    /// <summary><c>beforeunload</c>.</summary>
    BeforeUnload,
}

/// <summary>A native dialog as a test's handler sees it (upstream's <c>Dialog</c>).</summary>
public sealed class Dialog
{
    private readonly Func<string?, Task> _accept;
    private readonly Func<Task> _dismiss;

    internal Dialog(DialogType type, string message, Func<string?, Task> accept, Func<Task> dismiss)
    {
        Type = type;
        Message = message;
        _accept = accept;
        _dismiss = dismiss;
    }

    /// <summary>The native dialog kind, independent of its message.</summary>
    public DialogType Type { get; }

    /// <summary>The dialog's text.</summary>
    public string Message { get; }

    /// <summary>Accepts the dialog once. <paramref name="text"/> answers a prompt.</summary>
    public Task AcceptAsync(string? text = null) => _accept(text);

    /// <summary>Dismisses the dialog once.</summary>
    public Task DismissAsync() => _dismiss();
}

/// <summary>
/// What answers a native dialog (upstream's <c>DialogHandler</c>): <see cref="Accept"/>, <see cref="Dismiss"/>, or a function
/// that must call <see cref="Dialog.AcceptAsync"/> or <see cref="Dialog.DismissAsync"/> before it returns.
/// </summary>
public sealed class DialogHandler
{
    private DialogHandler(bool? accept, Func<Dialog, Task>? handler)
    {
        AcceptsAll = accept;
        Function = handler;
    }

    /// <summary>Accepts every dialog.</summary>
    public static DialogHandler Accept { get; } = new(true, null);

    /// <summary>Dismisses every dialog.</summary>
    public static DialogHandler Dismiss { get; } = new(false, null);

    internal bool? AcceptsAll { get; }

    internal Func<Dialog, Task>? Function { get; }

    /// <summary>A handler that runs <paramref name="handler"/> for each dialog.</summary>
    public static implicit operator DialogHandler(Func<Dialog, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new DialogHandler(null, handler);
    }
}

/// <summary>
/// Native dialog routing for one attempt (upstream's <c>DialogRouter</c>, <c>web/src/dialogs.ts</c>). Handlers are registered by the
/// <c>browser</c> fixture; the session owns the router so a dialog that fires before a test ever touches <c>browser</c> is still
/// accounted for instead of silently dismissed.
/// </summary>
internal sealed class DialogRouter(ErrorLatch latch)
{
    private readonly Lock _gate = new();
    private List<object> _registrations = [];
    private readonly Dictionary<object, DialogHandler> _handlers = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Registers a handler (the newest wins) and returns its idempotent unsubscribe. A registration is tracked by its own identity,
    /// so registering the same handler twice and unsubscribing once leaves one in place.
    /// </summary>
    public Action Add(DialogHandler handler)
    {
        var registration = new object();
        lock (_gate)
        {
            _handlers[registration] = handler;
            _registrations = [.. _registrations, registration];
        }

        return () =>
        {
            lock (_gate)
            {
                _handlers.Remove(registration);
                _registrations = [.. _registrations.Where(candidate => !ReferenceEquals(candidate, registration))];
            }
        };
    }

    /// <summary>Routes one native dialog to the newest registered handler; the attempt end waits for it.</summary>
    public Task DispatchAsync(IDialog dialog)
    {
        var work = RouteAsync(dialog);
        latch.Track("dialog", work);
        return work;
    }

    private async Task RouteAsync(IDialog dialog)
    {
        DialogHandler? handler;
        lock (_gate)
        {
            handler = _registrations.Count == 0 ? null : _handlers[_registrations[^1]];
        }

        if (handler is null)
        {
            latch.Latch(new EngineException(EngineErrorCodes.InvalidState, $"unhandled {dialog.Type} dialog: {dialog.Message}", retryable: false));
            await DismissQuietlyAsync(dialog).ConfigureAwait(false);
            return;
        }

        var decided = false;
        var answered = false;
        var shown = new Dialog(
            TypeOf(dialog.Type),
            dialog.Message,
            async text =>
            {
                decided = true;
                await dialog.AcceptAsync(text).ConfigureAwait(false);
                answered = true;
            },
            async () =>
            {
                decided = true;
                await dialog.DismissAsync().ConfigureAwait(false);
                answered = true;
            });
        try
        {
            if (handler.AcceptsAll is true)
            {
                await dialog.AcceptAsync().ConfigureAwait(false);
            }
            else if (handler.AcceptsAll is false)
            {
                await dialog.DismissAsync().ConfigureAwait(false);
            }
            else
            {
                await handler.Function!(shown).ConfigureAwait(false);

                // A dialog left open blocks the page, and the failure would surface later as an unrelated action timeout. The
                // handler contract is the same as a route handler's: decide, or the step fails naming you.
                if (!decided)
                {
                    await DismissQuietlyAsync(dialog).ConfigureAwait(false);
                    latch.Latch(new EngineException(
                        EngineErrorCodes.InvalidState,
                        $"dialog handler returned without calling accept or dismiss for {dialog.Type} dialog: {dialog.Message}",
                        retryable: false));
                }
            }
        }
        catch (Exception cause)
        {
            // A failed assertion or a policy refusal keeps its code; only an unclassified throw is the engine's to name.
            latch.Latch(cause is E2EException
                ? cause
                : new EngineException(EngineErrorCodes.EngineFailure, "dialog handler failed: " + WebErrors.Message(cause), retryable: false, cause));
            if (!answered)
            {
                await DismissQuietlyAsync(dialog).ConfigureAwait(false);
            }
        }
    }

    private static DialogType TypeOf(string type) => type switch
    {
        "alert" => DialogType.Alert,
        "confirm" => DialogType.Confirm,
        "prompt" => DialogType.Prompt,
        _ => DialogType.BeforeUnload,
    };

    private static async Task DismissQuietlyAsync(IDialog dialog)
    {
        try
        {
            await dialog.DismissAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            // The dialog is already answered or its page is gone.
        }
    }
}
