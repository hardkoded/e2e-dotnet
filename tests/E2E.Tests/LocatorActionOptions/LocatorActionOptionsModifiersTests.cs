// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.LocatorActionOptions;

/// <summary>
/// The "modifiers" describe of <c>locator-action-options.test.ts</c>, over a screen whose engine declares
/// <see cref="EngineCapabilities.TapModifiers"/> or not. Not ported: "refuses modifiers with a position", because the port has no tap
/// position yet; and the cases "quotes a modifier JSON cannot serialize" and the non-array value of "rejects modifiers %j before any
/// lookup", because a typed list cannot be either.
/// </summary>
public sealed class LocatorActionOptionsModifiersTests
{
    [Theory]
    [InlineData("tap")]
    [InlineData("click")]
    [InlineData("doubleTap")]
    [InlineData("dblclick")]
    [InlineData("secondaryTap")]
    public async Task Modifiers_hand_the_held_keys_to_an_engine_that_declares_tapModifiers(string verb)
    {
        var (screen, received) = LocatorActionOptionsTests.BoxScreen(tapModifiers: true);
        var options = new ClickOptions { Modifiers = [KeyModifier.Shift] };
        var locator = screen.GetByLabel("Agree");

        await (verb switch
        {
            "tap" => locator.TapAsync(options),
            "click" => locator.ClickAsync(options),
            "doubleTap" => locator.DoubleTapAsync(options),
            "dblclick" => locator.DblClickAsync(options),
            _ => locator.SecondaryTapAsync(options),
        });

        var action = Assert.Single(received);
        var held = verb switch
        {
            "tap" or "click" => Assert.IsType<LocatorAction.Tap>(action).Modifiers,
            "doubleTap" or "dblclick" => Assert.IsType<LocatorAction.DoubleTap>(action).Modifiers,
            _ => Assert.IsType<LocatorAction.SecondaryTap>(action).Modifiers,
        };
        Assert.Equal([KeyModifier.Shift], held);
    }

    [Fact]
    public async Task Modifiers_leave_the_field_off_an_action_that_names_no_modifier_an_empty_list_included()
    {
        var (screen, received) = LocatorActionOptionsTests.BoxScreen();

        await screen.GetByLabel("Agree").TapAsync(new ClickOptions { Modifiers = [] });
        await screen.GetByLabel("Agree").DoubleTapAsync();

        Assert.Equal<LocatorAction>([new LocatorAction.Tap(), new LocatorAction.DoubleTap()], received);
    }

    [Fact]
    public async Task Modifiers_are_refused_on_an_engine_that_does_not_declare_tapModifiers_before_it_acts()
    {
        var (screen, received) = LocatorActionOptionsTests.BoxScreen();

        var error = await Assert.ThrowsAsync<EngineException>(() => screen.GetByLabel("Agree").TapAsync(new ClickOptions { Modifiers = [KeyModifier.Shift] }));

        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        Assert.Empty(received);
    }

    [Fact]
    public async Task Modifiers_are_refused_before_resolving_so_a_missing_node_is_not_NOT_FOUND()
    {
        var (screen, _) = LocatorActionOptionsTests.BoxScreen();
        var started = DateTime.UtcNow;

        var error = await Assert.ThrowsAsync<EngineException>(() => screen.GetByLabel("Missing").SecondaryTapAsync(new ClickOptions { Modifiers = [KeyModifier.Alt] }));

        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromMilliseconds(500));
    }

    [Theory]
    [InlineData(new[] { KeyModifier.Shift, KeyModifier.Shift }, "names a modifier twice")]
    [InlineData(new[] { (KeyModifier)99 }, "modifiers must be an array of")]
    public async Task Modifiers_are_rejected_before_any_lookup(KeyModifier[] modifiers, string message)
    {
        var (screen, received) = LocatorActionOptionsTests.BoxScreen(tapModifiers: true);

        var error = await Assert.ThrowsAsync<TestException>(() => screen.GetByLabel("Agree").TapAsync(new ClickOptions { Modifiers = modifiers }));

        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.Contains(message, error.Message);
        Assert.Empty(received);
    }
}
