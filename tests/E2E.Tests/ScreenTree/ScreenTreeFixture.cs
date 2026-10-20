// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ScreenTree;

/// <summary>Upstream's <c>todoScreen</c> (<c>tests/unit/screen-tree.test.ts</c>): a screen over a list and a button, with a count of its observations and its steps.</summary>
internal sealed class ScreenTreeFixture
{
    private int _observed;

    public ScreenTreeFixture()
    {
        var list = new SemanticNode
        {
            Ref = "list",
            Role = "list",
            Name = "Todos",
            Children = [new SemanticNode { Ref = "milk", Role = "checkbox", Name = "Milk", States = new NodeStates { Checked = true } }],
        };
        var add = new SemanticNode { Ref = "add", Role = "button", Name = "Add" };
        var observation = new Observation { Route = "/", Roots = [new SemanticNode { Ref = "root", Role = "root", Children = [list, add] }] };
        Steps = new StepRecorder("attempt");
        Screen = new Screen(
            _ =>
            {
                Interlocked.Increment(ref _observed);
                return Task.FromResult(observation);
            },
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2))
        {
            Steps = Steps,
        };
    }

    public Screen Screen { get; }

    public StepRecorder Steps { get; }

    public int Observed => Volatile.Read(ref _observed);
}
