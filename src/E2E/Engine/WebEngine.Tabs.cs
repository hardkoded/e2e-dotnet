// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// One tab of the attempt (upstream's <c>AttemptTab</c>, <c>web/src/attempt-session.ts</c>): the attempt's own page, a tab the app opened
/// from one of the attempt's tabs (a <c>target="_blank"</c> link, <c>window.open</c>), or one a test opened. The object outlives a CDP
/// reconnect, which hands it the page of the same target on the new connection.
/// </summary>
internal sealed class AttemptTab(IPage page, AttemptTab? opener, WebEngine.TargetIdentity? target) : IAttemptTab
{
    /// <summary>The tab's page; a reconnect replaces it with the page of the same target.</summary>
    public IPage Page { get; set; } = page;

    /// <summary>The tab it was opened from; null for the attempt's own page, a tab a test opened, and one whose opener closed before it loaded.</summary>
    public AttemptTab? Opener { get; } = opener;

    /// <summary>The page's CDP target on a persistent browser, which a reconnect finds it by.</summary>
    public WebEngine.TargetIdentity? Target { get; set; } = target;

    /// <summary>When the tab was last shown, in the attempt's order; 0 for never.</summary>
    public long ShownAt { get; set; }

    /// <inheritdoc />
    public bool IsClosed => Page.IsClosed;
}

/// <summary>
/// The tab half of the engine (upstream <c>attempt-session.ts</c> tabs, and the surface's <c>switchTab</c>, <c>closeTabAt</c>, <c>tabs</c>,
/// <c>showTab</c>, <c>newTab</c>, <c>closeTab</c>, and <c>nextPopup</c>): the attempt's open tabs in the order they opened, the one shown, and the
/// changes between them.
/// </summary>
public sealed partial class WebEngine
{
    private sealed partial class WebSession
    {
        /// <summary>How long a page the app opened waits for the tab that opened it to be heard, before it is read as one nobody claimed.</summary>
        private static readonly TimeSpan OrphanTurn = TimeSpan.FromMilliseconds(100);

        private readonly object _tabsLock = new();

        /// <summary>The attempt's open tabs, in the order they opened; the session's page is the one shown.</summary>
        private List<AttemptTab> _tabs = [];

        /// <summary>How many times a tab was shown, for <see cref="AttemptTab.ShownAt"/>.</summary>
        private long _shownCount;

        /// <summary>
        /// Tab changes in the order they happened: following a tab the app opened, showing another, returning from one that closed. Settles once
        /// the last has finished; never faults. Every operation waits for it first.
        /// </summary>
        private Task _tabWork = Task.CompletedTask;

        /// <summary>Waiting for the next tab the app opens (<c>browser.waitForPopup</c>), each told the tab and the work that shows it.</summary>
        private readonly List<Action<AttemptTab, Task>> _popupWaiters = [];

        /// <summary>Pages the attempt is opening itself (its own page, <c>newTab</c>); every other page of its context is one the app opened.</summary>
        private readonly HashSet<Task<IPage>> _creating = [];

        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<IPage, object> _ownPages = new();

        /// <summary>Pages the app opened that are already joining the tabs, from their opener's <c>popup</c> or as an orphan.</summary>
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<IPage, object> _claimed = new();

        /// <summary>Shows the open tab at <paramref name="index"/>, in opening order, for the agent's <c>switchTab</c> verb, and shows the screen after it (upstream's <c>switchTab</c>).</summary>
        public Task SwitchTabAsync(int index, OperationContext context, CancellationToken cancellationToken) =>
            ActedAsync(cancellationToken, () => GuardAsync("switch tab", cancellationToken, () => ShowTabAtAsync(index)));

        /// <summary>Closes the open tab at <paramref name="index"/>, in opening order, for the agent's <c>closeTab</c> verb, and shows the screen after it (upstream's <c>closeTabAt</c>).</summary>
        public Task CloseTabAsync(int index, OperationContext context, CancellationToken cancellationToken) =>
            ActedAsync(cancellationToken, () => GuardAsync("close tab", cancellationToken, () => CloseTabAtAsync(index)));

        // --- tabs shared with the browser fixture ---

        /// <summary>The attempt's open tabs, in the order they opened (upstream's <c>tabs</c>).</summary>
        public IReadOnlyList<IAttemptTab> Tabs()
        {
            ThrowPending();
            RequireContext();
            return [.. OpenTabs()];
        }

        public bool IsActiveTab(IAttemptTab tab) => ReferenceEquals(ShownTab(), tab);

        public Task<string> TabUrlAsync(IAttemptTab tab, CancellationToken cancellationToken) =>
            GuardAsync("url", cancellationToken, () => Task.FromResult(((AttemptTab)tab).Page.Url));

        public Task<string> TabTitleAsync(IAttemptTab tab, CancellationToken cancellationToken) =>
            GuardAsync("title", cancellationToken, async () =>
            {
                try
                {
                    return await ((AttemptTab)tab).Page.TitleAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (WebErrors.IsPlaywright(ex))
                {
                    throw WebErrors.Translate(ex, "title");
                }
            });

        /// <summary>Shows one of the attempt's tabs (<c>BrowserTab.ActivateAsync</c>).</summary>
        public Task ActivateTabAsync(IAttemptTab tab, CancellationToken cancellationToken) =>
            GuardAsync("tab", cancellationToken, () => ShowTabAsync((AttemptTab)tab));

        /// <summary>Opens a blank tab in the attempt's context and shows it (<c>browser.newTab</c>).</summary>
        public async Task<IAttemptTab> NewTabAsync(CancellationToken cancellationToken) =>
            await GuardAsync("tab", cancellationToken, NewTabAsync).ConfigureAwait(false);

        /// <summary>Closes one of the attempt's tabs (<c>BrowserTab.CloseAsync</c>); the tab shown next is shown before it resolves.</summary>
        public Task CloseTabAsync(IAttemptTab tab, CancellationToken cancellationToken) =>
            GuardAsync("tab", cancellationToken, () => CloseTabAsync((AttemptTab)tab));

        /// <summary>
        /// Runs a trigger and waits for the tab it opens to become the active one (upstream's <c>waitForTab</c>, <c>web/src/browser.ts</c>). The
        /// trigger is test code, so its errors keep their classification; the wait for the tab is bounded apart from it. A timeout names the
        /// page a tab was still loading, if any, as such a tab has no page to show yet.
        /// </summary>
        public async Task<IAttemptTab> WaitForPopupAsync(Func<Task> trigger, TimeSpan timeout, CancellationToken cancellationToken)
        {
            const string Api = "browser.waitForPopup";
            return await GuardAsync("popup", cancellationToken, async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var budget = Budget("popup", cancellationToken, timeout);
                var waitMs = (long)timeout.TotalMilliseconds;
                using var scope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Exception? triggerFailure = null;
                long? triggerMs = null;
                Func<string?> loading = static () => null;

                TestException TimedOut(Exception inner)
                {
                    var url = loading();
                    if (url is not null)
                    {
                        return new TestException("ACTION_FAILED", $"the tab the app opened did not load {url} within {waitMs}ms", inner);
                    }

                    return new TestException(
                        "ACTION_FAILED",
                        triggerMs is null
                            ? $"{Api} exceeded its timeout of {waitMs}ms"
                            : $"no tab opened within {waitMs}ms; the trigger resolved after {triggerMs}ms",
                        inner);
                }

                async Task RunTriggerAsync()
                {
                    var begun = _seams.Clock.GetTimestamp();
                    try
                    {
                        await trigger().ConfigureAwait(false);
                    }
                    catch (Exception cause)
                    {
                        triggerFailure = cause;
                        throw;
                    }

                    triggerMs = (long)_seams.Clock.GetElapsedTime(begun).TotalMilliseconds;
                }

                try
                {
                    var popup = NextPopupCore(scope.Token);
                    loading = popup.Loading;
                    ObserveFault(popup.Opened);
                    var running = RunTriggerAsync();

                    // A trigger abandoned at the deadline that fails later is already reported as the timeout.
                    ObserveFault(running);
                    await budget.WithinAsync(running).ConfigureAwait(false);
                    return (IAttemptTab)await budget.WithinAsync(popup.Opened).ConfigureAwait(false);
                }
                catch (Exception cause) when (triggerFailure is not null && ReferenceEquals(cause, triggerFailure))
                {
                    // The trigger is test code, so its errors keep their original classification.
                    throw;
                }
                catch (EngineException ex) when (ex.Code == EngineErrorCodes.OperationTimeout)
                {
                    throw TimedOut(ex);
                }
                catch (Exception cause) when (WebErrors.IsPlaywright(cause))
                {
                    throw WebErrors.Translate(cause, "popup");
                }
                finally
                {
                    await scope.CancelAsync().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>The attempt's open tabs, in the order they opened.</summary>
        internal IReadOnlyList<AttemptTab> OpenTabs()
        {
            lock (_tabsLock)
            {
                return [.. _tabs];
            }
        }

        /// <summary>The tab the attempt shows, if a page is open (upstream's <c>shownTab</c>).</summary>
        internal AttemptTab? ShownTab()
        {
            var page = _page;
            lock (_tabsLock)
            {
                return page is null ? null : _tabs.Find(tab => ReferenceEquals(tab.Page, page));
            }
        }

        /// <summary>Shows one open tab: every read and action from now on is on its page.</summary>
        private async Task ShowTabAsync(AttemptTab tab)
        {
            void Open()
            {
                bool listed;
                lock (_tabsLock)
                {
                    listed = _tabs.Contains(tab);
                }

                if (!listed || tab.Page.IsClosed)
                {
                    throw new TestException("ACTION_FAILED", "the tab is closed");
                }
            }

            Open();

            // Checked again in turn: a change queued before this one may be the tab closing.
            await InTurnAsync(async () =>
            {
                Open();
                await ShowAsync(tab).ConfigureAwait(false);
            }, rethrow: true).ConfigureAwait(false);
        }

        /// <summary>The tabs the observation reports, when more than one is open (upstream's <c>withTabs</c>); null with one tab, or when no open tab is shown.</summary>
        private EngineTabs? TabsReport()
        {
            var tabs = OpenTabs();
            var shown = ShownTab();
            var active = shown is null ? -1 : tabs.ToList().IndexOf(shown);
            return tabs.Count < 2 || active == -1 ? null : new EngineTabs(tabs.Count, active);
        }

        /// <summary>Shows the open tab at <paramref name="index"/> in opening order, as the agent's <c>switchTab</c> counts them.</summary>
        private Task ShowTabAtAsync(int index) => ShowTabAsync(TabAt(index));

        /// <summary>Closes the open tab at <paramref name="index"/> in opening order, as the agent's <c>closeTab</c> counts them; the last open tab stays.</summary>
        private async Task CloseTabAtAsync(int index)
        {
            var tab = TabAt(index);
            bool LastOpen()
            {
                lock (_tabsLock)
                {
                    return _tabs.All(other => ReferenceEquals(other, tab) || other.Page.IsClosed);
                }
            }

            if (LastOpen())
            {
                throw new TestException("ACTION_FAILED", $"tab {(index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)} is the only open tab");
            }

            // Checked again in turn: a change queued before this one may be another tab closing.
            await InTurnAsync(async () =>
            {
                if (LastOpen())
                {
                    throw new TestException("ACTION_FAILED", $"tab {(index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)} is the only open tab");
                }

                if (!tab.Page.IsClosed)
                {
                    await tab.Page.CloseAsync().ConfigureAwait(false);
                }
            }, rethrow: true).ConfigureAwait(false);
            await TabsSettledAsync().ConfigureAwait(false);
        }

        /// <summary>The open tab at <paramref name="index"/> in opening order, or an <c>ACTION_FAILED</c> the model reads.</summary>
        private AttemptTab TabAt(int index)
        {
            lock (_tabsLock)
            {
                if (index >= 0 && index < _tabs.Count)
                {
                    return _tabs[index];
                }

                var open = _tabs.Count;
                throw new TestException(
                    "ACTION_FAILED",
                    $"there is no tab {(index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}: {open.ToString(System.Globalization.CultureInfo.InvariantCulture)} tab{(open == 1 ? " is" : "s are")} open");
            }
        }

        /// <summary>Opens a new tab in the attempt's context, as a test asks, and shows it.</summary>
        private async Task<AttemptTab> NewTabAsync()
        {
            var context = RequireContext();
            _ = Page;
            var page = await CreatePageAsync(context).ConfigureAwait(false);
            var tab = Adopt(page, null, null);
            try
            {
                await InTurnAsync(() => ShowAsync(tab), rethrow: true).ConfigureAwait(false);
            }
            catch
            {
                // Closed as a tab, so a screen already moved to it moves back.
                await page.CloseAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                await TabsSettledAsync().ConfigureAwait(false);
                throw;
            }

            return tab;
        }

        /// <summary>Closes a tab; when it was the one shown, waits until the tab shown next is.</summary>
        private async Task CloseTabAsync(AttemptTab tab)
        {
            if (!tab.Page.IsClosed)
            {
                await tab.Page.CloseAsync().ConfigureAwait(false);
            }

            await TabsSettledAsync().ConfigureAwait(false);
        }

        /// <summary>Whether <paramref name="page"/> closed as a tab does, with the browser still connected, rather than with the connection.</summary>
        internal bool TabClosed(IPage page) => page.IsClosed && !_disposed && _context is not null && _browser.IsConnected;

        /// <summary>Settles once every tab change queued so far has finished.</summary>
        internal async Task TabsSettledAsync()
        {
            // .NET-only bound: upstream waits on the queue unbounded; a stuck tab change must not hang a close for good.
            try
            {
                await _tabWork.WaitAsync(_actionTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new EngineException(EngineErrorCodes.OperationTimeout, "tab change timed out", retryable: false);
            }
        }

        /// <summary>
        /// Waits for the next tab the app opens and shows it: <see cref="PopupWait.Opened"/> resolves with the tab once it is shown. <see cref="PopupWait.Loading"/>
        /// names the page a tab is still loading meanwhile, which the browser has not reported as a tab yet, for a wait that times out.
        /// </summary>
        private PopupWait NextPopupCore(CancellationToken cancellationToken)
        {
            var opened = new TaskCompletionSource<AttemptTab>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<AttemptTab, Task>? waiter = null;
            waiter = (tab, shown) =>
            {
                lock (_tabsLock)
                {
                    _popupWaiters.Remove(waiter!);
                }

                _ = shown.ContinueWith(
                    _ => opened.TrySetResult(tab),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            };
            lock (_tabsLock)
            {
                _popupWaiters.Add(waiter);
            }

            var registered = waiter;

            var loading = new List<IRequest>();
            void Started(object? _, IRequest request)
            {
                if (OpensTab(request))
                {
                    lock (loading)
                    {
                        loading.Add(request);
                    }
                }
            }

            void Ended(object? _, IRequest request)
            {
                lock (loading)
                {
                    loading.Remove(request);
                }
            }

            void Answered(object? _, IResponse response) => Ended(null, response.Request);

            var context = RequireContext();
            context.Request += Started;
            context.Response += Answered;
            context.RequestFailed += Ended;
            var forgotten = 0;
            void Forget()
            {
                if (Interlocked.Exchange(ref forgotten, 1) != 0)
                {
                    return;
                }

                lock (_tabsLock)
                {
                    _popupWaiters.Remove(registered);
                }

                context.Request -= Started;
                context.Response -= Answered;
                context.RequestFailed -= Ended;
            }

            var registration = cancellationToken.Register(Forget);
            var result = opened.Task.WaitAsync(cancellationToken).ContinueWith(
                task =>
                {
                    Forget();
                    registration.Dispose();
                    return task;
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap();
            return new PopupWait(result, () =>
            {
                lock (loading)
                {
                    return loading.Count == 0 ? null : loading[^1].Url;
                }
            });
        }

        /// <summary>Opens a page of the attempt's own, which never counts as a tab the app opened (upstream's <c>createPage</c>).</summary>
        private Task<IPage> CreatePageAsync(IBrowserContext context)
        {
            async Task<IPage> Create()
            {
                var page = await context.NewPageAsync().ConfigureAwait(false);
                _ownPages.AddOrUpdate(page, new object());
                return page;
            }

            var creating = Create();
            lock (_tabsLock)
            {
                _creating.Add(creating);
            }

            _ = creating.ContinueWith(
                done =>
                {
                    lock (_tabsLock)
                    {
                        _creating.Remove(done);
                    }

                    return done.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return creating;
        }

        /// <summary>Records a page as one of the attempt's tabs and watches it for the tabs it opens and for its own close (upstream's <c>adopt</c>).</summary>
        private AttemptTab Adopt(IPage page, AttemptTab? opener, TargetIdentity? target)
        {
            var tab = new AttemptTab(page, opener, target);
            page.SetDefaultTimeout(ActionMs);
            lock (_tabsLock)
            {
                _tabs.Add(tab);
            }

            WatchTab(tab);
            return tab;
        }

        /// <summary>Listens on the tab's current page; a page a reconnect replaced stops counting for it.</summary>
        private void WatchTab(AttemptTab tab)
        {
            var page = tab.Page;
            page.Popup += (_, popup) => Follow(popup, tab, page);
            page.Close += (_, _) => Closed(tab, page);
        }

        /// <summary>A tab the app opened from one of the attempt's tabs joins the tabs.</summary>
        private void Follow(IPage popup, AttemptTab opener, IPage from)
        {
            bool listed;
            lock (_tabsLock)
            {
                listed = _tabs.Contains(opener);
            }

            if (!ReferenceEquals(opener.Page, from) || !listed)
            {
                return;
            }

            _claimed.AddOrUpdate(popup, new object());
            Join(popup, opener, null);
        }

        /// <summary>
        /// A page of the attempt's context that neither the attempt opened nor a tab heard open: one whose opener closed before Playwright
        /// reported it, such as a window that opens the next one and closes itself. When the browser says a page opened it, it joins the tabs
        /// like any tab the app opens; a page something else opened in the context does not. Read once the attempt's own pages being opened
        /// are known, and a turn after the <c>popup</c> event, which Playwright emits right after the context's <c>page</c> (upstream's <c>orphaned</c>).
        /// </summary>
        private void Orphaned(IPage page)
        {
            bool Unclaimed() => !_ownPages.TryGetValue(page, out _) && !_claimed.TryGetValue(page, out _);

            _ = Task.Run(async () =>
            {
                try
                {
                    Task[] creating;
                    lock (_tabsLock)
                    {
                        creating = [.. _creating];
                    }

                    foreach (var pending in creating)
                    {
                        await pending.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    }

                    await Task.Delay(OrphanTurn, CancellationToken.None).ConfigureAwait(false);
                    if (!Unclaimed() || _disposed)
                    {
                        return;
                    }

                    TargetIdentity target;
                    try
                    {
                        target = await TargetIdentityAsync(page).ConfigureAwait(false);
                    }
                    catch (Exception) when (!page.IsClosed || page.Context.Browser?.BrowserType.Name != "chromium")
                    {
                        // No CDP to ask (Firefox, WebKit): a page the attempt did not open is taken as the app's.
                        if (Unclaimed())
                        {
                            _claimed.AddOrUpdate(page, new object());
                            Join(page, null, null);
                        }

                        return;
                    }

                    if (target.OpenerId is null || !Unclaimed())
                    {
                        return;
                    }

                    _claimed.AddOrUpdate(page, new object());
                    Join(page, null, target);
                }
                catch (Exception)
                {
                    // A page that went away while it was read joins nothing.
                }
            });
        }

        /// <summary>
        /// Adds a page the app opened to the tabs once it has a document. It stays in the background, as the attempt's screen stays on its
        /// tab, unless <c>browser.waitForPopup</c> is waiting for it: then it is shown (upstream's <c>join</c>).
        /// </summary>
        private void Join(IPage popup, AttemptTab? opener, TargetIdentity? target)
        {
            if (_recovery is { IsCompleted: false } recovery)
            {
                // A reconnect is publishing the binding the page belongs to: join it then.
                _ = recovery.ContinueWith(
                    _ => Join(popup, opener, target),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.Default);
                return;
            }

            bool known;
            lock (_tabsLock)
            {
                known = _tabs.Any(tab => ReferenceEquals(tab.Page, popup));
            }

            if (_context is null || _disposed || popup.IsClosed || known)
            {
                return;
            }

            if (popup.Url.Length == 0)
            {
                // No document committed yet: a link whose response was a download leaves its tab so, where a person's browser closes it. It
                // joins the tabs only once a document commits.
                EventHandler<IFrame>? committed = null;
                committed = (_, _) =>
                {
                    if (popup.Url.Length == 0)
                    {
                        return;
                    }

                    popup.FrameNavigated -= committed;
                    Join(popup, opener, target);
                };
                popup.FrameNavigated += committed;
                return;
            }

            if (_persistent is not null && target is null)
            {
                // A reconnect finds each tab by its target, a tab in the background too, so a tab joins with it.
                _ = TargetIdentityAsync(popup).ContinueWith(
                    read => Join(popup, opener, read.Result),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.Default);
                return;
            }

            var joined = Adopt(popup, opener, target);
            Action<AttemptTab, Task>[] waiters;
            lock (_tabsLock)
            {
                waiters = [.. _popupWaiters];
            }

            if (waiters.Length == 0)
            {
                return;
            }

            var shown = InTurnAsync(() => ShowAsync(joined));
            foreach (var waiter in waiters)
            {
                waiter(joined, shown);
            }
        }

        /// <summary>
        /// A tab's page closed. When it was the one shown, its opener is shown next, else the open tab shown most recently. A page a dropped
        /// connection closed is no tab closing: the close is read a turn later, once the browser says whether it is still connected, and
        /// recovery keeps the tab (upstream's <c>closed</c>).
        /// </summary>
        private void Closed(AttemptTab tab, IPage page)
        {
            // Queued now, so an operation that starts before the close is read waits for it.
            var turn = InTurnAsync(async () =>
            {
                await Task.Yield();
                await Task.Delay(OrphanTurn / 10, CancellationToken.None).ConfigureAwait(false);
                ApplyClose(tab, page);
            });
            _ = turn;
        }

        private void ApplyClose(AttemptTab tab, IPage page)
        {
            AttemptTab? next;
            lock (_tabsLock)
            {
                if (!ReferenceEquals(tab.Page, page) || !_tabs.Contains(tab) || _context is null || _disposed || !_browser.IsConnected)
                {
                    return;
                }

                _tabs.Remove(tab);
                if (!ReferenceEquals(_page, page))
                {
                    return;
                }

                next = ReturnTo(tab);
                if (next is null)
                {
                    _refs.Clear();
                    _page = null;
                }
                else
                {
                    Publish(next);
                }
            }

            _ = InTurnAsync(async () =>
            {
                await VideoPageClosedAsync().ConfigureAwait(false);
                if (next is null || !ReferenceEquals(ShownTab(), next))
                {
                    return;
                }

                if (_viewport is { } size)
                {
                    await next.Page.SetViewportSizeAsync(size.Width, size.Height).ConfigureAwait(false);
                }

                await VideoPageOpenedAsync(next.Page).ConfigureAwait(false);
            });
        }

        /// <summary>The tab shown when <paramref name="closed"/> closes: its nearest open opener, else the open tab shown most recently (upstream's <c>returnTo</c>).</summary>
        private AttemptTab? ReturnTo(AttemptTab closed)
        {
            for (var opener = closed.Opener; opener is not null; opener = opener.Opener)
            {
                if (_tabs.Contains(opener))
                {
                    return opener;
                }
            }

            AttemptTab? best = null;
            foreach (var tab in _tabs)
            {
                if (best is null || tab.ShownAt > best.ShownAt)
                {
                    best = tab;
                }
            }

            return best;
        }

        /// <summary>
        /// Shows a tab once its page is set up as the attempt's own is: the viewport a test asked for, the media features, its target on a
        /// persistent browser. Then the recording moves to it (upstream's <c>show</c>).
        /// </summary>
        private async Task ShowAsync(AttemptTab tab)
        {
            var context = RequireContext();
            var page = tab.Page;
            if (ReferenceEquals(_page, page))
            {
                return;
            }

            if (_viewport is { } size)
            {
                await page.SetViewportSizeAsync(size.Width, size.Height).ConfigureAwait(false);
            }

            await (_emulated.TryGetValue(page, out var emulated) ? emulated : ApplyMediaAsync(page)).ConfigureAwait(false);
            if (_persistent is not null)
            {
                tab.Target ??= await TargetIdentityAsync(page).ConfigureAwait(false);
            }

            lock (_tabsLock)
            {
                if (!ReferenceEquals(_context, context) || _disposed || !_tabs.Contains(tab) || page.IsClosed)
                {
                    return;
                }

                Publish(tab);
            }

            await VideoPageOpenedAsync(page).ConfigureAwait(false);
        }

        /// <summary>Makes <paramref name="tab"/> the session's page: every reference and read in flight belongs to the page shown before (upstream's <c>publish</c>).</summary>
        private void Publish(AttemptTab tab)
        {
            _refs.Clear();
            tab.ShownAt = Interlocked.Increment(ref _shownCount);
            _target = tab.Target ?? _target;
            _page = tab.Page;
        }

        /// <summary>
        /// Queues one tab change behind the earlier ones. The queue never faults; <paramref name="rethrow"/> hands this change's own failure
        /// to its caller, and a change nobody awaits fails quietly, as a tab the app opened cannot fail a step (upstream's <c>inTurn</c>).
        /// </summary>
        private Task InTurnAsync(Func<Task> work, bool rethrow = false)
        {
            Task run;
            lock (_tabsLock)
            {
                var before = _tabWork;
                run = Task.Run(
                    async () =>
                    {
                        await before.ConfigureAwait(false);
                        await work().ConfigureAwait(false);
                    },
                    CancellationToken.None);
                _tabWork = run.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            return rethrow ? run : run.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        /// <summary>
        /// After a reconnect, finds each tab's page on the new connection by its target and listens there; a tab whose target is gone is closed
        /// (upstream's <c>remapTabs</c>).
        /// </summary>
        private async Task RemapTabsAsync(IBrowserContext context)
        {
            var byTarget = new Dictionary<string, IPage>(StringComparer.Ordinal);
            foreach (var page in context.Pages)
            {
                byTarget[(await TargetIdentityAsync(page).ConfigureAwait(false)).TargetId] = page;
            }

            lock (_tabsLock)
            {
                _tabs = [.. _tabs.Where(tab =>
                {
                    if (tab.Target is null || !byTarget.TryGetValue(tab.Target.TargetId, out var page))
                    {
                        return false;
                    }

                    tab.Page = page;
                    WatchTab(tab);
                    return true;
                })];
            }
        }

        /// <summary>Forgets every tab, for a context or page set the session closes itself.</summary>
        private void ForgetTabs()
        {
            lock (_tabsLock)
            {
                _tabs = [];
            }
        }

        /// <summary>
        /// Waits for the tab changes already queued (a tab that closed, one a test showed) before an operation reads or acts on a tab, within
        /// the operation's own time.
        /// </summary>
        private async Task SettleTabsAsync(string label, CancellationToken cancellationToken)
        {
            if (_tabWork.IsCompleted)
            {
                return;
            }

            try
            {
                await _tabWork.WaitAsync(_actionTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new EngineException(EngineErrorCodes.OperationTimeout, label + " timed out", retryable: false);
            }
        }

        /// <summary>
        /// Runs a read of the tab the attempt shows. A tab the app closes under the read makes it stale, as a navigation does: nothing was
        /// dispatched, and the read repeats on the tab shown next (upstream's <c>readingTab</c>).
        /// </summary>
        private async Task<T> ReadingTabAsync<T>(Func<Task<T>> read, CancellationToken cancellationToken)
        {
            var page = OpenPage();
            try
            {
                return await read().ConfigureAwait(false);
            }
            catch (Exception cause) when (page is null ? false : !cancellationToken.IsCancellationRequested && cause is not EngineException { Code: EngineErrorCodes.Cancelled } && TabClosed(page))
            {
                throw new EngineException(EngineErrorCodes.NodeStale, "the tab closed while it was read", retryable: true, cause);
            }
        }

        /// <summary>The first request of a tab being opened: a navigation Playwright has no frame for yet, since the page it loads in is not reported (upstream's <c>opensTab</c>).</summary>
        private static bool OpensTab(IRequest request)
        {
            if (!request.IsNavigationRequest || !(request.Url.StartsWith("http:", StringComparison.Ordinal) || request.Url.StartsWith("https:", StringComparison.Ordinal)))
            {
                return false;
            }

            try
            {
                _ = request.Frame;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    /// <summary>The wait for the next tab the app opens (upstream's <c>nextPopup</c> result).</summary>
    /// <param name="Opened">Resolves with the tab once it is shown.</param>
    /// <param name="Loading">Names the page a tab is still loading meanwhile, for a wait that times out; null when none is.</param>
    internal sealed record PopupWait(Task<AttemptTab> Opened, Func<string?> Loading);
}
