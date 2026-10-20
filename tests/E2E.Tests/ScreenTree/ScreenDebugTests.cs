// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ScreenTree;

/// <summary>Upstream's <c>describe('screen.debug')</c> (<c>tests/unit/screen-tree.test.ts</c>).</summary>
[Collection(nameof(ConsoleCollection))]
public sealed class ScreenDebugTests
{
    [Fact]
    public async Task Prints_the_tree_to_the_test_output()
    {
        var fixture = new ScreenTreeFixture();
        var printed = new StringWriter();
        var original = Console.Out;
        Console.SetOut(printed);
        try
        {
            await fixture.Screen.DebugAsync();
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal(await fixture.Screen.TreeAsync() + Environment.NewLine, printed.ToString());
        var step = fixture.Steps.All()[0];
        Assert.Equal("screen.debug", step.Api);
        Assert.Equal(StepStatus.Passed, step.Status);
    }
}

/// <summary>Tests that swap <see cref="Console.Out"/> run one at a time.</summary>
[CollectionDefinition(nameof(ConsoleCollection), DisableParallelization = true)]
public sealed class ConsoleCollection;
