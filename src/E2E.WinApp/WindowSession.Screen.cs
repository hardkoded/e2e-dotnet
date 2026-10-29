// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.WinApp;

public sealed partial class WindowSession
{
    /// <summary>
    /// Reads the app's windows and projects them under the screen root. The main window comes first; every other visible window
    /// of the process (a flyout, a menu, a combo box's list, a tooltip: WinUI puts each in a popup window of its own) follows, in
    /// the same coordinate space, so the model sees the layer that is open. A session before <c>app.open()</c> has no screen to
    /// read, and the step says so.
    /// </summary>
    public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        var app = _app ?? throw WinAppSupport.InvalidState("no app window is open; call app.open()");
        var windows = await CommandAsync("observe", (driver, token) => driver.WindowsAsync(app.ProcessId, token), cancellationToken).ConfigureAwait(false);
        var main = windows.FirstOrDefault(window => window.Handle == app.MainWindow) ?? Largest(windows)
            ?? throw WinAppSupport.InvalidState($"{app.Name} has no window open; the app may have exited, call app.open()");
        _mainWindow = main.Handle;
        _scale = main.Scale > 0 ? main.Scale : 1;
        var origin = new Point(main.Bounds.X, main.Bounds.Y);
        var viewport = new ViewportSize(main.Bounds.Width / _scale, main.Bounds.Height / _scale);
        _viewport = viewport;

        var focused = await CommandAsync("observe", (driver, token) => driver.FocusedAsync(main.Handle, token), cancellationToken).ConfigureAwait(false);
        var budget = new ProjectionBudget(ObservationLimits.Nodes);
        var roots = new List<SemanticNode>();
        var bindings = new List<NodeBinding>();
        var truncated = false;
        foreach (var window in windows.OrderBy(window => window.Handle == main.Handle ? 0 : 1))
        {
            var raw = await CommandAsync("observe", (driver, token) => driver.InspectAsync(window.Handle, token), cancellationToken).ConfigureAwait(false);
            var projected = UiaProjection.ProjectWindow(raw, window.Handle, origin, _scale, focused, () => $"n{++_idCounter}", budget);
            roots.Add(projected.Node);
            truncated |= projected.Truncated;
            bindings.AddRange(projected.Index.Select(entry => new NodeBinding(entry.Id, entry.Raw.Handle, entry.WindowHandle, entry.Node, entry.Raw, entry.Role)));
            if (budget.Exhausted)
            {
                truncated = true;
                break;
            }
        }

        Remember(bindings);
        Route = WinAppSupport.ScreenLocation(app.Name, main.Title) ?? "";
        return new Observation
        {
            Route = Route,
            Roots = [UiaProjection.ScreenRoot(roots, viewport)],
            Viewport = viewport.Width >= 0.5 && viewport.Height >= 0.5 ? new TraceViewport((int)Math.Round(viewport.Width), (int)Math.Round(viewport.Height)) : null,
            Truncated = truncated,
            KeyboardVisible = null,
        };
    }

    /// <summary>The newest binding of a secure field, by role and state, for the screenshot mask. Every binding of the last observation is read.</summary>
    internal IReadOnlyList<Rect> SecureRects()
    {
        var latest = Latest;
        return [.. latest.Where(entry => entry.Node.States.Secure && entry.Raw.Bounds is not null).Select(entry => entry.Raw.Bounds!.Value)];
    }

    /// <summary>True when a secure field of the last observation has no box: the mask cannot cover it, so the screenshot is withheld.</summary>
    internal bool HasUnmaskableSecureField() => Latest.Any(entry => entry.Node.States.Secure && entry.Raw.Bounds is null);
}
