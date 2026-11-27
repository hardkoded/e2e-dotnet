// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Desktop;

internal sealed partial class DesktopSurface
{
    /// <summary>How many node bindings stay usable; the oldest go first.</summary>
    private const int MaxBindings = 4096;

    /// <summary>Codes a read fails with while the app has no window to read: not launched yet, still opening, or just closed.</summary>
    private static readonly HashSet<string> NoWindowCodes = ["APP_NOT_FOUND", "WINDOW_NOT_FOUND"];

    /// <summary>How much sooner than the call's cut-off the driver stops a read, so a read cut short still answers with what it read.</summary>
    private const int ReadAnswerMarginMs = 200;

    /// <summary>Stands for a sheet the driver gave no key: it is never known to be the same sheet as any.</summary>
    private const string UnknownSheet = "?";

    /// <summary>
    /// Projects a snapshot of one window, its node identities scoped to that window. An identity of that window a complete snapshot no longer has
    /// is retired: a control that went, or now has a look-alike sibling, never passes its id on to another. A snapshot cut short retires only the
    /// identities it saw a look-alike take. Another window's identities stay, so its controls keep their ids when it is read again.
    /// </summary>
    private ProjectedScreen ProjectWindow(RawSnapshot raw, bool minimized, bool interactive)
    {
        // An interactive read prunes the tree, a whole one does not: each keeps its own identities, so one never retires the other's ids.
        var scope = $"{raw.Window?.Id ?? ""}|{(interactive ? "i" : "w")}|";
        var used = new HashSet<string>(StringComparer.Ordinal);
        var shared = new HashSet<string>(StringComparer.Ordinal);
        var screen = DesktopNodes.ProjectSnapshot(
            raw,
            identity =>
            {
                if (identity is null)
                {
                    return IdFor(null);
                }

                var scoped = $"{scope}{identity}";
                used.Add(scoped);
                return IdFor(scoped);
            },
            new ProjectionOptions
            {
                Minimized = minimized,
                Interactive = interactive,
                OnLookAlike = identity => shared.Add($"{scope}{identity}"),
                PriorIdOf = identity => _ids.GetValueOrDefault($"{scope}{identity}"),
            }
        );
        // A read that ran out of time saw only part of the window: what it did not reach may still be there, so only an identity it saw a twin take retires.
        var partial = raw.Complete == false || raw.Truncated == true;
        foreach (var identity in _ids.Keys.ToList())
        {
            if (
                identity.StartsWith(scope, StringComparison.Ordinal)
                && !used.Contains(identity)
                && (!partial || shared.Contains(identity))
            )
            {
                _ids.Remove(identity);
            }
        }

        return screen;
    }

    // --- reading the screen ---

    /// <summary>
    /// One snapshot of a window of the app the engine launched, projected: the window <paramref name="windowId"/> names, else the one the test chose
    /// (and no other while it is chosen), else the focused one, the visible ones, and the rest, in that order, the first one accessibility can read.
    /// <paramref name="forMask"/> is a read that only places secure fields: it reads every node even with <c>snapshot: 'interactive'</c>, which leaves
    /// out a secure node with no action of its own, and hands out no ids, so the ids of the screen the harness holds stay as they are.
    /// <paramref name="whole"/> reads every node too, with ids, for a locator: a label left out of an interactive read would match nothing, and a
    /// negative assertion on it pass. Null, an empty screen, before <c>app.open()</c> and while the running app has no window; a copy of the app the
    /// engine did not launch is never read. A read that fails is no empty screen, which would let a negative assertion pass: an app that quit is
    /// <c>INVALID_STATE</c>, as is a chosen window that closed, and windows none of which could be read, or none listed while the screen is locked,
    /// are a retryable <c>NODE_STALE</c>, so a locator reads again until its deadline.
    /// </summary>
    private async Task<ProjectedScreen?> CaptureAsync(
        DesktopOperation operation,
        string? windowId = null,
        bool forMask = false,
        bool whole = false
    )
    {
        // As upstream's default parameter: no window named reads the one the test chose.
        windowId ??= _chosenWindow;
        var windows = await OwnWindowsAsync(operation, strict: true).ConfigureAwait(false);
        var chosen = windowId is not null && windowId == _chosenWindow;
        if (chosen && !windows.Any(window => window.Id == windowId))
        {
            UnlessLocked();
            throw ChosenClosed();
        }

        if (windows.Count == 0 && _pid is not null)
        {
            UnlessLocked();
        }

        // A window the list no longer has is closed: its identities go, so windows opened and closed one after another do not pile them up.
        var open = new HashSet<string>(windows.Select(window => window.Id), StringComparer.Ordinal);
        foreach (var identity in _ids.Keys.ToList())
        {
            if (!open.Contains(identity[..Math.Max(identity.IndexOf('|', StringComparison.Ordinal), 0)]))
            {
                _ids.Remove(identity);
            }
        }

        var candidates = windowId is null ? ByPreference(windows) : [.. windows.Where(window => window.Id == windowId)];
        // Only a dialog macOS put over the app is open: no screen of the app's own, and no empty one either.
        if (candidates.Count == 0 && windows.Any(window => window.System == true))
        {
            throw new EngineException(
                "NODE_STALE",
                $"only a system dialog is over {_appName ?? "the app"}; reading again (desktop.useWindow() reads the dialog itself)",
                retryable: true,
                Unreadable
            );
        }

        var pid = _pid;
        foreach (var window in candidates)
        {
            if (pid is null || window.Accessible == false)
            {
                continue;
            }

            var interactive = Options.InteractiveOnly && !forMask && !whole;
            try
            {
                var raw = await ReadAsync(
                        (driver, timeoutMs) =>
                            driver.SnapshotAsync(
                                new SnapshotRequest(pid.Value, window.Id, Options.MaxDepth, interactive, timeoutMs)
                            ),
                        new DriverCall
                        {
                            Label = "snapshot",
                            Input = false,
                            Signal = operation.Signal,
                            TimeoutMs = operation.TimeoutMs,
                        }
                    )
                    .ConfigureAwait(false);
                if (!forMask)
                {
                    var projected = ProjectWindow(raw, window.Minimized == true, interactive);
                    var sheet = SheetKey(window);
                    return sheet is null ? projected : projected with { Sheet = sheet };
                }

                var scratch = 0;
                return DesktopNodes.ProjectSnapshot(raw, _ =>
                    "mask" + (++scratch).ToString(System.Globalization.CultureInfo.InvariantCulture)
                );
            }
            catch (DriverFailure cause) when (NoWindowCodes.Contains(cause.Code))
            {
                // A panel accessibility cannot read, or a window that just closed: try the next one.
                if (chosen)
                {
                    UnlessLocked();
                    throw ChosenClosed();
                }
            }
            catch (Exception cause) when (cause is not OperationCanceledException)
            {
                throw DesktopDriverCalls.Translate(cause, "snapshot", false);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        // A locked screen still lists the app's windows, none of them readable: say so rather than that each closed.
        UnlessLocked();
        throw new EngineException(
            "NODE_STALE",
            $"no window of {_appName ?? "the app"} could be read through accessibility, or each closed while it was read",
            retryable: true,
            Unreadable
        );
    }

    /// <summary>
    /// Throws a retryable <c>NODE_STALE</c> while the Mac's screen is locked: macOS then lists the app's windows as unreadable, or none of them,
    /// which must not read as a window that closed or an app with none open.
    /// </summary>
    private void UnlessLocked()
    {
        if (RequireDriver().ScreenLocked() != true)
        {
            return;
        }

        throw new EngineException(
            "NODE_STALE",
            $"the Mac's screen is locked, so no window of {_appName ?? "the app"} can be read through accessibility; reading again",
            retryable: true,
            Unreadable
        );
    }

    /// <summary>Throws <c>INVALID_STATE</c> unless the process the engine launched still runs.</summary>
    private async Task RequireRunningAsync()
    {
        if (_pid is not { } pid || await RunningAsync(pid).ConfigureAwait(false))
        {
            return;
        }

        throw new EngineException(
            "INVALID_STATE",
            $"{_appName ?? "the app"} quit (process {pid}); open it again with app.open()",
            retryable: false,
            Quit
        );
    }

    /// <summary>
    /// The windows of the process the engine launched; none before <c>app.open()</c> or after the app quit. <paramref name="strict"/> is for a read of
    /// the screen, where an app that quit must not pass for an empty screen: it is <c>INVALID_STATE</c>.
    /// </summary>
    private async Task<IReadOnlyList<RawWindow>> OwnWindowsAsync(DesktopOperation operation, bool strict = false)
    {
        if (_pid is not { } pid)
        {
            return [];
        }

        // An id another process took over since the app quit names none of its windows.
        if (!await RunningAsync(pid).ConfigureAwait(false))
        {
            if (strict)
            {
                await RequireRunningAsync().ConfigureAwait(false);
            }

            return [];
        }

        try
        {
            var windows = await ReadAsync(
                    (driver, _) => driver.ListWindowsAsync(pid),
                    new DriverCall
                    {
                        Label = "list windows",
                        Input = false,
                        Signal = operation.Signal,
                        TimeoutMs = operation.TimeoutMs,
                    }
                )
                .ConfigureAwait(false);
            return [.. windows.Where(window => window.Pid is null || window.Pid == pid)];
        }
        catch (DriverFailure cause) when (NoWindowCodes.Contains(cause.Code))
        {
            if (strict)
            {
                await RequireRunningAsync().ConfigureAwait(false);
                // The process still runs, so the list failed, not the app: no empty screen, which would let a negative assertion pass.
                throw new EngineException(
                    "NODE_STALE",
                    $"the windows of {_appName ?? "the app"} could not be listed while it runs; reading again",
                    retryable: true,
                    Unreadable
                );
            }

            return [];
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw DesktopDriverCalls.Translate(cause, "list windows", false);
        }
    }

    /// <summary>The window of the launched process a command reads or acts in: the one the test chose, else the first by <see cref="ByPreference"/>.</summary>
    private async Task<RawWindow?> OwnWindowAsync(DesktopOperation operation)
    {
        var windows = await OwnWindowsAsync(operation).ConfigureAwait(false);
        return _chosenWindow is not null
            ? windows.FirstOrDefault(window => window.Id == _chosenWindow)
            : ByPreference(windows).FirstOrDefault();
    }

    /// <summary>Upstream's <c>observe</c>.</summary>
    public async Task<Observation> ObserveAsync(DesktopOperation context, ObserveOptions? options)
    {
        var operation = context.WithDeadline();
        var screen = await CaptureAsync(operation).ConfigureAwait(false);
        if (screen is null)
        {
            return new Observation
            {
                Route = "",
                Roots = [new SemanticNode { Ref = DesktopNodes.RootId, Role = "window" }],
                Viewport = new TraceViewport(0, 0),
            };
        }

        Remember(screen);
        var withheld = false;
        (EngineScreenshot Pixels, int Masked)? pixels = null;
        if (options?.Pixels == true)
        {
            try
            {
                pixels = await PixelsForAsync(screen, operation).ConfigureAwait(false);
            }
            catch (EngineException cause) when (cause.Code is "OPERATION_TIMEOUT" or "CANCELLED")
            {
                // Out of time or cancelled is no screenshot at all, and the observation with it.
                throw;
            }
            catch (Exception cause) when (cause is not OperationCanceledException)
            {
                // A capture that failed (no Screen Recording permission) is no screenshot, as on the mobile engine, not masking that could not be proven.
                withheld = ReferenceEquals(cause.InnerException, Unmasked);
            }
        }

        _pointWindow = PointIn(screen);
        // Pixels withheld for their secure fields still report the regions masked, none, so the harness reads masking it could not prove
        // (MASKING_UNPROVEN), not a missing screenshot.
        var snapshot = SnapshotOf(screen);
        return new Observation
        {
            Route = snapshot.Route,
            Location = snapshot.Location,
            Roots = snapshot.Roots,
            Viewport = snapshot.Viewport,
            Truncated = snapshot.Truncated,
            Pixels = pixels?.Pixels,
            MaskedRegionCount = pixels is null ? (withheld ? 0 : null) : null,
        };
    }

    private Observation SnapshotOf(ProjectedScreen screen)
    {
        var app = _appName;
        var location =
            app is null ? screen.Title
            : screen.Title is null ? app
            : $"{app} › {screen.Title}";
        return new Observation
        {
            Route = location ?? "",
            Location = location,
            Roots = [Bounded(screen.Root)],
            Viewport = new TraceViewport((int)Math.Round(screen.ViewportWidth), (int)Math.Round(screen.ViewportHeight)),
            Truncated = screen.Truncated,
        };
    }

    /// <summary>Upstream's <c>locate</c>.</summary>
    public async Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorExpression expression, DesktopOperation context)
    {
        ArgumentNullException.ThrowIfNull(expression);
        // A field's placeholder names it when it has no label, so a placeholder query would read the same text twice; it is refused, as before.
        if (UsesPlaceholder(expression))
        {
            throw Unsupported(
                "macOS names a field with no label by its placeholder, so find it with getByRole('textbox', name) or getByLabel(name)"
            );
        }

        var operation = context.WithDeadline();
        // A locator reads the whole window whatever `snapshot` says, as on the mobile engine: the interactive read leaves out static text.
        var screen = await CaptureAsync(operation, null, forMask: false, whole: true).ConfigureAwait(false);
        if (screen is null)
        {
            return [];
        }

        if (_attempt?.Screen is { } sink)
        {
            try
            {
                sink(SnapshotOf(screen));
            }
            catch (Exception)
            {
                // The trace is evidence; a sink that throws never fails the step.
            }
        }

        // A read the deadline stopped is the locator's time running out: it answers as the timeout it is, so the wait ends on what its last whole
        // read found (core never takes a cut read for an answer).
        if (screen.TimedOut)
        {
            throw new EngineException(
                "OPERATION_TIMEOUT",
                "the window could not be read whole before the deadline",
                retryable: false
            );
        }

        // A read cut short (by a stall, or at the depth limit) can leave out a match: no match would not prove a control absent, and one would not
        // prove it the only one. The locator reads again until its deadline.
        if (screen.Truncated)
        {
            throw new EngineException(
                "NODE_STALE",
                $"the window was read only in part (the app did not answer for part of it, it is deeper than the {Options.MaxDepth} levels a snapshot reads, which desktop({{ maxDepth }}) raises, or it holds more than the {MaxSnapshotNodes} elements it reads), so the matches cannot be counted",
                retryable: true
            );
        }

        var matches = ExpressionResolution.ResolveExpression(expression, [screen.Root]);
        _pointWindow = PointIn(screen);
        Remember(matches.Select(match => screen.Bindings.GetValueOrDefault(match.Ref)).OfType<NodeBinding>());
        return matches;
    }

    /// <summary>Whether an expression, or one it is built from, queries by placeholder.</summary>
    private static bool UsesPlaceholder(LocatorExpression expression) =>
        expression.Kind == "placeholder"
        || (expression.Parent is { } parent && UsesPlaceholder(parent))
        || (expression.Source is { } source && UsesPlaceholder(source))
        || (expression.Has is { } has && UsesPlaceholder(has))
        || (expression.HasNot is { } hasNot && UsesPlaceholder(hasNot))
        || (expression.Left is { } left && UsesPlaceholder(left))
        || (expression.Right is { } right && UsesPlaceholder(right));

    // --- helpers ---

    private NodeBinding BindingOf(string id)
    {
        if (!_bindings.TryGetValue(id, out var binding))
        {
            throw new EngineException("NODE_STALE", $"node {id} is not on the screen", retryable: true);
        }

        // A node read before desktop.useWindow() chose another window is not on the screen in use: the harness locates it again there.
        if (_chosenWindow is not null && binding.WindowId != _chosenWindow)
        {
            throw new EngineException(
                "NODE_STALE",
                $"node {id} is in another window than the one desktop.useWindow() chose",
                retryable: true
            );
        }

        return binding;
    }

    private void Remember(ProjectedScreen screen) => Remember(screen.BindingOrder.Select(id => screen.Bindings[id]));

    /// <summary>
    /// Keeps the newest binding of each node: an id names the same control in every snapshot, and the newest snapshot's ref is the one to act on. A
    /// control that has since gone keeps its last ref, which the driver refuses as stale.
    /// </summary>
    private void Remember(IEnumerable<NodeBinding> bindings)
    {
        var fresh = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (_bindings.Remove(binding.Id))
            {
                _bindingOrder.Remove(binding.Id);
            }

            _bindings[binding.Id] = binding;
            _bindingOrder.Add(binding.Id);
            fresh.Add(binding.Id);
        }

        // The oldest go first, but never one just read: a screen larger than the cap keeps every node.
        foreach (var oldest in _bindingOrder.ToList())
        {
            if (_bindings.Count <= MaxBindings)
            {
                break;
            }

            if (!fresh.Contains(oldest))
            {
                _bindings.Remove(oldest);
                _bindingOrder.Remove(oldest);
            }
        }
    }

    /// <summary>
    /// The app's windows in the order a command reads them when the test chose none: the focused one, the main one (an app in the background may
    /// have no focused window), the visible ones, the rest. A dialog macOS adds (Siri's, which macOS 27 shows over an app after a press in a
    /// SwiftUI sheet, and lists first) is left out, though it may take focus: read in place of an unreadable window of the app, it would let a
    /// negative assertion pass. <c>desktop.useWindow</c> still picks it.
    /// </summary>
    private static List<RawWindow> ByPreference(IReadOnlyList<RawWindow> windows)
    {
        static int Rank(RawWindow window) =>
            window.IsFocused == true ? 0
            : window.IsMain == true ? 1
            : window.Visible != false ? 2
            : 3;
        return [.. windows.Where(window => window.System != true).OrderBy(Rank)];
    }

    /// <summary>The window a screen was read from, and the sheet that held its focus then: where a point taken in it was taken.</summary>
    private static PointWindow? PointIn(ProjectedScreen screen) =>
        screen.WindowId is null ? null : new PointWindow(screen.WindowId, screen.Sheet);

    /// <summary>The sheet that holds a window's focus: its key, <see cref="UnknownSheet"/> when the driver gave none, null for no sheet.</summary>
    private static string? SheetKey(RawWindow window) =>
        window.SheetFocused == true ? window.SheetId ?? UnknownSheet : null;

    /// <summary>Whether two sheet keys name the same sheet: both none, or both sheets with the same key. A sheet with no key matches none.</summary>
    private static bool SameSheet(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return a == b;
        }

        return a != UnknownSheet && a == b;
    }

    /// <summary>
    /// An observation tree with each field cut at the contract's limit, the end dropped: a name at <see cref="ObservationLimits.Name"/>, text, a
    /// value, and a selection at <see cref="ObservationLimits.Text"/>. The harness reads a field exactly at its limit as one that may have been
    /// cut (and redacts a secret it may end with), and one long label can no longer crowd the controls after it out of what the agent reads.
    /// <c>locate</c> matches on the whole tree, and single-node reads carry the whole value.
    /// </summary>
    private static SemanticNode Bounded(SemanticNode node)
    {
        static string? Cut(string? text, int limit) => text is not null && text.Length > limit ? text[..limit] : text;
        return new SemanticNode
        {
            Ref = node.Ref,
            Role = node.Role,
            Name = Cut(node.Name, ObservationLimits.Name),
            Text = Cut(node.Text, ObservationLimits.Text),
            Value = Cut(node.Value, ObservationLimits.Text),
            Selection = Cut(node.Selection, ObservationLimits.Text),
            TestId = node.TestId,
            Placeholder = node.Placeholder,
            InputPurpose = node.InputPurpose,
            Level = node.Level,
            States = node.States,
            Attributes = node.Attributes,
            Rect = node.Rect,
            Children = [.. node.Children.Select(Bounded)],
        };
    }

    /// <summary>Whether two rects are the same; two missing rects are not.</summary>
    private static bool SameRect(Rect? a, Rect? b) =>
        a is { } left
        && b is { } right
        && left.X == right.X
        && left.Y == right.Y
        && left.Width == right.Width
        && left.Height == right.Height;

    /// <summary>Whether two lists of secure-field rects are the same, in order.</summary>
    private static bool SameRects(IReadOnlyList<Rect>? a, IReadOnlyList<Rect> b) =>
        a is not null && a.Count == b.Count && a.Zip(b).All(pair => SameRect(pair.First, pair.Second));
}
