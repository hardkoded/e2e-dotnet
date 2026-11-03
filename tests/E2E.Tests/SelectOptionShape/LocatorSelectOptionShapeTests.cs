// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.SelectOptionShape;

/// <summary>
/// Upstream's <c>select-option-shape.test.ts</c>, "locator.selectOption shape". The port's <see cref="SelectOption"/> is a typed
/// class, so the cases that need a value of another shape are not ported: "rejects an array of options before the engine acts" (a
/// call takes one <see cref="SelectOption"/>), and, of "rejects %s before the engine acts", an unknown key, a non-string label, a
/// non-string value, a fractional index (the index is an <see cref="int"/>), a label inheriting an index, and a label beside a
/// non-enumerable value. Two keys and a negative index are ported; "none of the three" is added with them (.NET-only).
/// </summary>
public sealed class LocatorSelectOptionShapeTests
{
    private static readonly SemanticNode Frequency = new() { Ref = "frequency", Role = "listbox", Name = "Frequency" };

    /// <summary>A screen over one select, logging every action the engine is asked to perform.</summary>
    private static (Locator Select, List<LocatorAction> Performed) SelectScreen()
    {
        var performed = new List<LocatorAction>();
        var observation = new Observation { Route = "/", Roots = [Frequency] };
        var screen = new Screen(
            _ => Task.FromResult(observation),
            (_, action, _) =>
            {
                performed.Add(action);
                return Task.CompletedTask;
            },
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromMilliseconds(300),
            TimeSpan.FromMilliseconds(300));
        return (screen.GetByRole("listbox", "Frequency"), performed);
    }

    [Fact]
    public async Task Hands_value_to_the_engine_unchanged()
    {
        var (select, performed) = SelectScreen();
        var option = new SelectOption { Value = "daily" };

        await select.SelectOptionAsync(option);

        Assert.Equal<LocatorAction>([new LocatorAction.Select(option)], performed);
    }

    [Fact]
    public async Task Hands_index_to_the_engine_unchanged()
    {
        var (select, performed) = SelectScreen();
        var option = new SelectOption { Index = 0 };

        await select.SelectOptionAsync(option);

        Assert.Equal<LocatorAction>([new LocatorAction.Select(option)], performed);
    }

    [Fact]
    public async Task NET_only_hands_label_to_the_engine_unchanged()
    {
        var (select, performed) = SelectScreen();
        var option = new SelectOption { Label = "Daily" };

        await select.SelectOptionAsync(option);

        Assert.Equal<LocatorAction>([new LocatorAction.Select(option)], performed);
    }

    public static TheoryData<string, SelectOption> Refused => new()
    {
        { "two keys", new SelectOption { Label = "Daily", Value = "daily" } },
        { "a negative index", new SelectOption { Index = -1 } },
        { "NET-only none", new SelectOption() },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task Rejects_x_before_the_engine_acts(string name, SelectOption option)
    {
        Assert.NotEmpty(name);
        var (select, performed) = SelectScreen();

        var error = await Assert.ThrowsAsync<TestException>(() => select.SelectOptionAsync(option));

        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.Equal("selectOption takes a label string or exactly one of { label }, { value }, { index } with a nonnegative integer index", error.Message);
        Assert.Empty(performed);
    }
}
