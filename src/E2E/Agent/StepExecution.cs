// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E;

/// <summary>What <c>StepAccounting</c> is built from (upstream's <c>StepAccountingOptions</c>).</summary>
internal sealed record StepAccountingOptions(int MaxActions, int MaxModelCalls, string Api);

/// <summary>
/// What <c>ActionDispatcher</c> is built from (upstream's <c>ActionDispatcherOptions</c>); <see cref="Instruction"/> also sizes the
/// observation feed's budget.
/// </summary>
internal sealed record ActionDispatcherOptions(
    Func<IReadOnlyList<Secret>> Secrets,
    string Instruction = "",
    JsonObject? Params = null,
    ActionHook? OnAction = null,
    BeforeActionHook? OnBeforeAction = null,
    bool ReadOnly = false
);

/// <summary>What <see cref="StepExecution.CreateStepExecution"/> builds a step from (upstream's <c>StepExecutionOptions</c>).</summary>
/// <param name="Accounting">The clock, the budgets, and the hard stop.</param>
/// <param name="Observation">The observation feed's options; its instruction bytes are measured from <see cref="Actions"/>.</param>
/// <param name="Actions">The verb table's options.</param>
internal sealed record StepExecutionOptions(
    StepAccountingOptions Accounting,
    ObservationFeedOptions Observation,
    ActionDispatcherOptions Actions
);

/// <summary>The executor context members the step machinery backs (upstream's <c>StepExecutionBindings</c>).</summary>
/// <param name="Budgets">The step's budgets, which are its accounting.</param>
/// <param name="Actions">The step's verb table.</param>
internal sealed record StepExecutionBindings(IExecutorBudgets Budgets, ActionDispatcher Actions);

/// <summary>The machinery of one step (upstream's <c>StepExecution</c>): its accounting, its observation feed, and the bindings.</summary>
internal sealed record StepExecution(StepAccounting Accounting, ObservationFeed Feed, StepExecutionBindings Bindings)
{
    /// <summary>Builds the step machinery and the executor context members it backs (upstream's <c>createStepExecution</c>).</summary>
    public static StepExecution CreateStepExecution(AttemptScope scope, StepExecutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(options);
        var accounting = new StepAccounting(
            options.Accounting.MaxActions,
            options.Accounting.MaxModelCalls,
            options.Accounting.Api
        );
        var feed = new ObservationFeed(scope.Session, TimeProvider.System)
        {
            Scope = scope,
            Accounting = accounting,
            Options = options.Observation with
            {
                InstructionBytes = ModelAdapter.TokenUpperBound(options.Actions.Instruction),
            },
        };
        var actions = options.Actions;
        var dispatcher = new ActionDispatcher(
            scope,
            feed,
            accounting,
            actions.Secrets,
            actions.OnAction,
            actions.Instruction,
            actions.Params,
            actions.OnBeforeAction,
            actions.ReadOnly
        );
        return new StepExecution(accounting, feed, new StepExecutionBindings(accounting, dispatcher));
    }
}
