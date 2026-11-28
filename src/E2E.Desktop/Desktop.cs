// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Desktop;

/// <summary>
/// The <c>desktop</c> fixture: what a test asks of the desktop itself rather than of a control on screen. Each call is recorded as a
/// <c>desktop.&lt;method&gt;</c> step bounded by the action timeout. Upstream's <c>Desktop</c> interface and <c>createDesktopFixture</c> in
/// <c>desktop.ts</c>; the port's fixture is a class, and each method reads the cancellation token of its own call, so a fixture follows a harness
/// from the test body into its teardown.
/// </summary>
public sealed class Desktop
{
    private readonly DesktopSurface _surface;
    private readonly Func<EngineFixtureContext?> _context;
    private readonly TimeSpan _actionTimeout;

    internal Desktop(DesktopSurface surface, Func<EngineFixtureContext?> context, TimeSpan actionTimeout)
    {
        _surface = surface;
        _context = context;
        _actionTimeout = actionTimeout;
    }

    /// <summary>The OS the app runs on: <c>macos</c>.</summary>
    public string Platform => _surface.Platform;

    /// <summary>The app's open windows, the focused one marked.</summary>
    public Task<IReadOnlyList<DesktopWindow>> WindowsAsync(CancellationToken cancellationToken = default) =>
        RunAsync("desktop.windows", "", cancellationToken, operation => _surface.WindowsAsync(operation));

    /// <summary>
    /// Reads and acts in this window from now on, rather than the focused one, until the app quits: a panel or bubble that takes focus then cannot
    /// hide the window the test is about.
    /// </summary>
    public Task UseWindowAsync(DesktopWindow window, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        return UseWindowAsync(window.Id, cancellationToken);
    }

    /// <summary>
    /// <see cref="UseWindowAsync(DesktopWindow, CancellationToken)"/> with the window's id, as <see cref="DesktopWindow.Id"/> gives it.
    /// Upstream's <c>useWindow</c> takes either.
    /// </summary>
    public Task UseWindowAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return RunAsync("desktop.useWindow", "", cancellationToken, async operation =>
        {
            await _surface.UseWindowAsync(id, operation).ConfigureAwait(false);
            return true;
        });
    }

    /// <summary>Goes back to reading and acting in the focused window. Upstream's <c>useWindow()</c> with no window; .NET-only overload for the omitted argument.</summary>
    public Task UseWindowAsync(CancellationToken cancellationToken = default) =>
        RunAsync("desktop.useWindow", "", cancellationToken, async operation =>
        {
            await _surface.UseWindowAsync(null, operation).ConfigureAwait(false);
            return true;
        });

    /// <summary>
    /// Presses one key, or a combo such as <c>ControlOrMeta+2</c>, into the app's window, for an app with no control that takes keyboard focus.
    /// Without <c>Headed</c> the window must be the app's key window.
    /// </summary>
    public Task PressAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return RunAsync("desktop.press", key, cancellationToken, async operation =>
        {
            await _surface.PressKeyAsync(key, operation).ConfigureAwait(false);
            return true;
        });
    }

    /// <summary>Quits the app. <c>app.open()</c> launches it again; every test ends with it quit.</summary>
    public Task CloseAppAsync(CancellationToken cancellationToken = default) =>
        RunAsync("desktop.closeApp", "", cancellationToken, async operation =>
        {
            await _surface.CloseAppAsync(operation).ConfigureAwait(false);
            return true;
        });

    /// <summary>Runs one fixture call as a recorded <c>resource</c> step of the attempt, or as it is for a session no test harness started.</summary>
    private Task<T> RunAsync<T>(
        string api,
        string label,
        CancellationToken cancellationToken,
        Func<DesktopOperation, Task<T>> body
    )
    {
        var context = _context();
        Task<T> Run() =>
            body(DesktopOperation.Of(context?.Operation() ?? OperationContext.Test, _actionTimeout, cancellationToken));
        return context is null ? Run() : context.RunOperationAsync(StepKind.Resource, api, label, Run);
    }
}

/// <summary>
/// What a session on a <see cref="DesktopEngine"/> holds beside <c>Screen</c> and <c>App</c>: the <see cref="Desktop"/> fixture. Read it from
/// <see cref="Session{TFixtures}.Fixtures"/> on a host launched through <see cref="Launch.LaunchAsync{TFixtures}"/>. Upstream's
/// <c>DesktopFixtures</c> (<c>fixtures.ts</c>).
/// </summary>
public sealed class DesktopFixtures
{
    internal DesktopFixtures(Desktop desktop) => Desktop = desktop;

    /// <summary>What a test asks of the desktop itself: its windows, a key into the app, quitting it.</summary>
    public Desktop Desktop { get; }
}
