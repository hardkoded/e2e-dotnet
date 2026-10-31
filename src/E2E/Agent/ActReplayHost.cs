// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;

namespace E2E;

public sealed partial class Agent
{
    /// <summary>
    /// The host an act replays a recording through (upstream builds it in <c>agent/step-cache.ts</c>, as <c>StepCacheHost</c>). Its actions perform one
    /// recorded action on the engine and arm the change wait its settle policy asks for, like the dispatcher's, but take no
    /// action slot and record nothing: the replay takes the slot and records the action as the recording holds it, with the
    /// slots of its params in it. Values arrive as recorded and are expanded with this call's params here.
    /// </summary>
    private sealed class ActReplayHost(
        AttemptScope scope,
        ObservationFeed feed,
        StepAccounting budget,
        ActionDispatcher dispatcher,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken token) : IStepCacheHost, IExecutorActions
    {
        public IExecutorActions Actions => this;

        public CancellationToken Signal => token;

        public Redactor Redact => scope.Redactor;

        public TimeSpan ReplayTimeout => scope.ReplayTimeout;

        public int RemainingMs() => budget.RemainingMs();

        public TimeProvider Time => TimeProvider.System;

        public Observation? FirstActedOn => feed.FirstActedOn;

        public Task<Observation> ObserveAsync(SettleMode mode) => feed.ObserveAsync(mode, token);

        public bool TryReserve() => budget.TryReserve();

        public void MarkActing() => feed.MarkActing();

        public Task CommitAsync(RecordedAction action)
        {
            budget.Recorder.Record(action);
            return dispatcher.NotifyAsync(action, ActionSource.Replay);
        }

        public Task TapAsync(ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken = default) => ClickAsync("tap", target, options, cancellationToken);

        public Task DoubleTapAsync(ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken = default) => ClickAsync("doubleTap", target, options, cancellationToken);

        private Task ClickAsync(string kind, ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken) =>
            PerformAsync(target, ActionDispatcher.ClickAction(kind, ActionDispatcher.RequireClickModifiers(options, kind, scope.EngineCapabilities)), kind, cancellationToken);

        // A value is expanded with this call's params before it is read: one that expands to a secret's marker fills that secret.
        public Task TypeAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default)
        {
            var expanded = CacheKeys.Detemplate(value, parameters);
            return expanded.StartsWith("<secret:", StringComparison.Ordinal) && expanded.EndsWith('>')
                ? TypeSecretAsync(target, expanded["<secret:".Length..^1], cancellationToken, armed: null)
                : PerformAsync(target, new LocatorAction.Fill(expanded, false), "fill", cancellationToken);
        }

        public Task TypeSecretAsync(ExecutorTarget target, string secretName, CancellationToken cancellationToken = default) =>
            TypeSecretAsync(target, secretName, cancellationToken, armed: "<secret:" + secretName + ">");

        private Task TypeSecretAsync(ExecutorTarget target, string secretName, CancellationToken cancellationToken, string? armed)
        {
            var secret = scope.Secrets.FirstOrDefault(item => string.Equals(item.Name, secretName, StringComparison.Ordinal))
                ?? throw new AgentException("AUTH_CREDENTIAL_UNAVAILABLE", "Replay needs secret '" + secretName + "', and this step did not declare it.");
            return PerformAsync(target, new LocatorAction.Fill(secret.Value, false), "fill", cancellationToken, armed);
        }

        public Task ClearAsync(ExecutorTarget target, CancellationToken cancellationToken = default) => PerformAsync(target, new LocatorAction.Clear(), "clear", cancellationToken);

        public async Task PressAsync(ExecutorTarget? target, string key, CancellationToken cancellationToken = default)
        {
            if (target is null)
            {
                await scope.Session.PressAsync(key, cancellationToken).ConfigureAwait(false);
                feed.ArmAfter(new RecordedAction { Kind = "press" });
                return;
            }

            await PerformAsync(target, new LocatorAction.Press(key), "press", cancellationToken).ConfigureAwait(false);
        }

        public Task SelectAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default) =>
            PerformAsync(target, new LocatorAction.Select(CacheKeys.Detemplate(value, parameters)), "select", cancellationToken);

        public Task CheckAsync(ExecutorTarget target, bool check, CancellationToken cancellationToken = default) =>
            check ? PerformAsync(target, new LocatorAction.Check(), "check", cancellationToken) : PerformAsync(target, new LocatorAction.Uncheck(), "uncheck", cancellationToken);

        // The recorded paths are expanded with this call's params, then authorized against the project root again, as the recording's were.
        public Task UploadAsync(ExecutorTarget target, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
        {
            var authorized = dispatcher.AuthorizeUpload([.. paths.Select(path => CacheKeys.Detemplate(path, parameters))]);
            return PerformAsync(target, new LocatorAction.SetInputFiles(authorized.Resolved), "upload", cancellationToken);
        }

        public Task ScrollToAsync(ExecutorTarget target, CancellationToken cancellationToken = default) => PerformAsync(target, new LocatorAction.ScrollIntoView(), "scrollTo", cancellationToken);

        public async Task ScrollUntilAsync(string text, ScrollDirection direction, ExecutorTarget? list = null, CancellationToken cancellationToken = default)
        {
            await dispatcher.PageUntilAsync(CacheKeys.Detemplate(text, parameters), direction, list, cancellationToken).ConfigureAwait(false);
            feed.ArmAfter(new RecordedAction { Kind = "scrollUntil" });
        }

        public async Task ScrollAsync(ExecutorTarget? list, ScrollDirection direction, int times = 1, CancellationToken cancellationToken = default)
        {
            for (var repeat = 0; repeat < times; repeat++)
            {
                if (list is null)
                {
                    await scope.Session.SwipeAsync(direction, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await scope.Session.PerformAsync(Resolve(list), new LocatorAction.Swipe(direction), cancellationToken).ConfigureAwait(false);
                }

                feed.ArmAfter(new RecordedAction { Kind = "scroll" });
            }
        }

        public async Task NavigateAsync(string url, CancellationToken cancellationToken = default)
        {
            await scope.Session.OpenAsync(Routes.Resolve(scope.BaseUrl, CacheKeys.Detemplate(url, parameters)), cancellationToken).ConfigureAwait(false);
            feed.ArmAfter(new RecordedAction { Kind = "navigate" });
        }

        public async Task BackAsync(CancellationToken cancellationToken = default)
        {
            await scope.Session.BackAsync(cancellationToken).ConfigureAwait(false);
            feed.ArmAfter(new RecordedAction { Kind = "back" });
        }

        // The node the replay found: a replay's targets are built from nodes, so an id alone names nothing here.
        private static SemanticNode Resolve(ExecutorTarget target) =>
            target.Node ?? throw new InvalidOperationException("A replayed target holds the node it was found as.");

        private async Task PerformAsync(ExecutorTarget target, LocatorAction action, string kind, CancellationToken cancellationToken, string? value = null)
        {
            await scope.Session.PerformAsync(Resolve(target), action, cancellationToken).ConfigureAwait(false);
            feed.ArmAfter(new RecordedAction { Kind = kind, Value = value });
        }
    }
}
