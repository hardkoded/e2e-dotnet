// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Cache;
using E2E.Internal;

namespace E2E;

/// <summary>
/// Upstream's <c>StepAccounting</c> (<c>agent/step-accounting.ts</c>): the clock, the budgets, and the books of one
/// dispatched step. Everything the runtime enforces against an executor is enforced here: the deadline and the stop that
/// ends the step, the action and model-call budgets, and the first hard stop, which outranks whatever the executor says
/// afterwards. The action slots are drawn on by replayed and live actions alike. <see cref="Recorder"/> holds the actions
/// the step took, in order, for its cache entry; the port has no step record, so the accounting keeps it.
/// </summary>
internal sealed class StepAccounting(int maxActions, int maxModelCalls, string api = "agent.act") : IExecutorBudgets
{
    private AttemptScope? _scope;
    private DateTimeOffset? _deadline;
    private TimeSpan _timeout;
    private CancellationToken _cancellation;
    private CancellationTokenSource? _stop;
    private AgentException? _hardStop;
    private TraceRecorder? _recorder;
    private Action<RecordedAction>? _committed;
    private int _prepaid;
    private bool _ended;

    public int MaxActions => maxActions;

    public int MaxModelCalls => maxModelCalls;

    public TraceRecorder Recorder => LazyInitializer.EnsureInitialized(ref _recorder, () => new TraceRecorder(() => _scope?.Redactor ?? Redactor.None));

    /// <summary>The one queue the step's observations and actions run in, in call order.</summary>
    public OperationQueue Queue { get; } = new();

    public int Used { get; private set; }

    public int ModelCalls { get; private set; }

    public bool Exhausted { get; private set; }

    /// <summary>The first budget, timeout, or cancel failure of the step. Runtime truth outranks the verdict.</summary>
    public AgentException? HardStop => Volatile.Read(ref _hardStop);

    public string Message => api + " exhausted its action budget of " + maxActions.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Puts the step on a clock. <paramref name="stop"/> is cancelled on a hard stop other than a spent budget, so the executor
    /// and every engine operation of the step end with it. <paramref name="cancellation"/> is the caller's and the attempt's,
    /// without the step's own deadline. <paramref name="committed"/> serves a project tool's gap.
    /// </summary>
    public void Attach(AttemptScope scope, TimeSpan timeout, CancellationToken cancellation, CancellationTokenSource stop, Action<RecordedAction> committed)
    {
        _scope = scope;
        _timeout = timeout;
        // A step with no timeout has no deadline.
        _deadline = timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan ? DateTimeOffset.UtcNow + timeout : null;
        _cancellation = cancellation;
        _stop = stop;
        _committed = committed;
    }

    /// <summary>Closes the books: what an executor that outlived the step reports afterwards belongs to no step.</summary>
    public void Close() => _ended = true;

    /// <summary>The token the step's own work runs under: it ends on a cancel and on every hard stop, a timeout included.</summary>
    public CancellationToken Token => _stop?.Token ?? CancellationToken.None;

    public int ActionsUsed() => Used;

    public int RemainingMs()
    {
        if (_deadline is not { } deadline)
        {
            return int.MaxValue;
        }

        var remaining = (deadline - DateTimeOffset.UtcNow).TotalMilliseconds;
        return remaining <= 0 ? 0 : remaining >= int.MaxValue ? int.MaxValue : (int)remaining;
    }

    /// <summary>
    /// Records the first hard stop. A timeout and a cancel also end the step's token: there is nothing left for the executor to
    /// say. A spent budget does not: the executor may still catch it and conclude with its own analysis, which the verdict
    /// mapping stamps with the runtime code, bounded by the deadline either way.
    /// </summary>
    public AgentException Fatalize(AgentException error)
    {
        Interlocked.CompareExchange(ref _hardStop, error, null);
        if (!string.Equals(error.Code, "STEP_BUDGET_EXHAUSTED", StringComparison.Ordinal))
        {
            try
            {
                _stop?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The step already ended.
            }
        }

        return error;
    }

    /// <summary>The timeout of the step as the error says it. Upstream's <c>STEP_TIMEOUT</c> message.</summary>
    public AgentException TimedOut() =>
        new("STEP_TIMEOUT", api + " exceeded its " + _timeout.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms timeout", blocked: true);

    /// <summary>Fails when the step is cancelled or out of time, and records the hard stop.</summary>
    public void Checkpoint()
    {
        if (_cancellation.IsCancellationRequested || (_scope?.Token().IsCancellationRequested ?? false))
        {
            throw Fatalize(new AgentException("CANCELLED", "the step was cancelled"));
        }

        if (_deadline is { } deadline && DateTimeOffset.UtcNow >= deadline)
        {
            throw Fatalize(TimedOut());
        }
    }

    /// <summary>Takes one action slot if the budget has one. A replay that runs out hands the step over rather than failing it.</summary>
    public bool TryReserve()
    {
        if (Used >= maxActions)
        {
            Exhausted = true;
            return false;
        }

        Used++;
        return true;
    }

    /// <summary>
    /// Takes one action slot for the tool call the tool loop is about to make, before the call is checked, so a call the
    /// loop turns away costs a slot as before. The one dispatcher action it makes spends the slot. False when the budget is spent.
    /// </summary>
    public bool TryPrepay()
    {
        if (!TryReserve())
        {
            return false;
        }

        _prepaid++;
        return true;
    }

    /// <summary>Drops a slot the call never spent.</summary>
    public void ClearPrepaid() => _prepaid = 0;

    /// <summary>
    /// Claims a slot before any side effect, for grammar actions and project tools alike. A failed action was still an
    /// attempt, so the slot is spent either way. Throws <c>STEP_BUDGET_EXHAUSTED</c> once the budget is spent.
    /// </summary>
    public void Reserve()
    {
        Checkpoint();
        if (_ended)
        {
            throw new AgentException("CANCELLED", "the step has ended");
        }

        if (_prepaid > 0)
        {
            _prepaid--;
            return;
        }

        if (!TryReserve())
        {
            throw Fatalize(new AgentException("STEP_BUDGET_EXHAUSTED", Message, blocked: true));
        }
    }

    public void RecordModelCall(ExecutorModelCall? usage = null)
    {
        // An executor the step abandoned can still report late. The step it belonged to is closed.
        if (_ended)
        {
            return;
        }

        ModelCalls++;
        if (_scope is { } scope)
        {
            scope.Steps.AddModelCall(usage);
            scope.ModelCalls++;
            scope.InputTokens += usage?.InputTokens ?? 0;
            scope.OutputTokens += usage?.OutputTokens ?? 0;
        }

        if (ModelCalls > maxModelCalls)
        {
            throw Fatalize(new AgentException("STEP_BUDGET_EXHAUSTED", api + " exhausted its model-call budget of " + maxModelCalls.ToString(CultureInfo.InvariantCulture), blocked: true));
        }
    }

    public async Task<T> RunToolAsync<T>(string name, bool mutates, Func<Task<T>> body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(body);
        if (!mutates)
        {
            Checkpoint();
            return await body().ConfigureAwait(false);
        }

        // A mutating tool takes an action slot and is recorded as a gap: the grammar cannot reproduce it, so a replay ends there and
        // hands the step to the executor. The gap is recorded before the tool runs, since a failed tool may have changed the app.
        // Mutations run one at a time with the grammar actions, in call order.
        return await Queue.RunAsync(async () =>
        {
            Reserve();
            var gap = Recorder.RecordGap(name);
            try
            {
                return await body().ConfigureAwait(false);
            }
            finally
            {
                _committed?.Invoke(gap);
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the runtime's own accounting corroborates a runtime code. Runtime codes are assigned by the runtime: the recorded
    /// hard stop vouches directly, and the clock, the budgets, and the cancellation vouch for an executor that saw exhaustion
    /// before the context machinery did.
    /// </summary>
    public bool Vouches(string code)
    {
        if (string.Equals(HardStop?.Code, code, StringComparison.Ordinal))
        {
            return true;
        }

        return code switch
        {
            "CANCELLED" => _cancellation.IsCancellationRequested || (_scope?.Token().IsCancellationRequested ?? false),
            "STEP_TIMEOUT" => _deadline is { } deadline && DateTimeOffset.UtcNow >= deadline,
            "STEP_BUDGET_EXHAUSTED" => Used >= maxActions || ModelCalls >= maxModelCalls,
            _ => false,
        };
    }

    public AgentException Stop(string? summary)
    {
        return new AgentException("STEP_BUDGET_EXHAUSTED", string.IsNullOrEmpty(summary) ? Message + "." : Message + ": " + summary, blocked: true);
    }
}
