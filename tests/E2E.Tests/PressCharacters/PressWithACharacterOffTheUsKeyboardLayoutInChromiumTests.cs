// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.PressCharacters.PressHarness;

namespace E2E.Tests.PressCharacters;

/// <summary>
/// <c>describe('press with a character off the US keyboard layout, in Chromium')</c> of upstream's
/// <c>packages/web/tests/integration/press-characters.test.ts</c>: <c>press</c> through the engine in a real browser. One page load per test,
/// as upstream's <c>beforeEach</c> opens it.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class PressWithACharacterOffTheUsKeyboardLayoutInChromiumTests : IAsyncLifetime
{
    private PressHarness _chromium = null!;

    public async Task InitializeAsync()
    {
        _chromium = await PressHarness.StartAsync(BrowserName.Chromium);
        await _chromium.OpenPageAsync();
    }

    public async Task DisposeAsync() => await _chromium.DisposeAsync();

    [Fact]
    public async Task Types_each_character_into_the_field_a_locator_addresses() => await TypesEachCharacterAsync(Open);

    [Fact]
    public async Task Types_each_character_into_the_field_an_element_handle_addresses() =>
        await TypesEachCharacterAsync(Labelled);

    [Fact]
    public async Task Sends_the_key_events_of_a_US_key()
    {
        await _chromium.PressAsync(Open, "e");
        await _chromium.PressAsync(Open, "é");
        Assert.Equal("keydown:e;input:e;keyup:e;keydown:é;input:é;keyup:é;", await _chromium.ValueOfAsync(Keys));
    }

    [Fact]
    public async Task Types_into_a_contenteditable_node()
    {
        await _chromium.PressAsync(Rich, "é");
        await _chromium.PressFocusedAsync("中");
        Assert.Equal("keydown:é;input:é;keyup:é;keydown:中;input:中;keyup:中;", await _chromium.ValueOfAsync(Keys));
    }

    [Fact]
    public async Task Types_into_the_focused_field()
    {
        await _chromium.PressAsync(Open, "a");
        await _chromium.PressFocusedAsync("é");
        Assert.Equal("aé", await _chromium.ValueOfAsync(Open));
    }

    [Fact]
    public async Task Types_into_a_field_of_an_iframe_by_node_and_by_focus()
    {
        await _chromium.PressAsync(Inner, "é");
        await _chromium.PressFocusedAsync("ß");
        Assert.Equal("éß", await _chromium.ValueOfAsync(Inner));
    }

    [Fact]
    public async Task Presses_the_key_on_a_button_which_types_nothing()
    {
        await _chromium.PressAsync(Go, "é");
        Assert.Equal("keydown:é;keyup:é;", await _chromium.ValueOfAsync(Keys));
    }

    [Fact]
    public async Task Leaves_a_number_input_to_refuse_the_character_itself()
    {
        await _chromium.PressAsync(Count, "5");
        await _chromium.PressAsync(Count, "é");
        Assert.Equal("5", await _chromium.ValueOfAsync(Count));
    }

    [Fact]
    public async Task Holds_a_modifier_over_the_key_on_a_node_and_releases_it() =>
        await HoldsModifierAsync((key) => _chromium.PressAsync(Open, key));

    [Fact]
    public async Task Holds_a_modifier_over_the_key_on_the_focused_field_and_releases_it() =>
        await HoldsModifierAsync(_chromium.PressFocusedAsync);

    [Fact]
    public async Task Types_the_character_with_Shift_held()
    {
        await _chromium.PressAsync(Open, "Shift+é");
        Assert.Equal("é", await _chromium.ValueOfAsync(Open));
        Assert.Contains("keydown:é:shift;", await _chromium.ValueOfAsync(Keys));
    }

    private async Task TypesEachCharacterAsync(LocatorExpression field)
    {
        foreach (var key in new[] { "e", "é", "ß", "€", "中", "😀" })
        {
            await _chromium.PressAsync(field, key);
        }

        Assert.Equal("eéß€中😀", await _chromium.ValueOfAsync(field));
    }

    private async Task HoldsModifierAsync(Func<string, Task> press)
    {
        await _chromium.PressAsync(Open, "a");
        await press("Control+é");
        await press("b");
        Assert.Equal("ab", await _chromium.ValueOfAsync(Open));
        Assert.Contains(
            "keydown:Control:ctrl;keydown:é:ctrl;keyup:é:ctrl;keyup:Control;keydown:b;",
            await _chromium.ValueOfAsync(Keys)
        );
    }
}
