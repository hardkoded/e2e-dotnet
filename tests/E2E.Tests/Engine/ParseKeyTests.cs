// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Engine;

/// <summary>
/// Upstream's <c>describe('parseKey')</c> in <c>engine.test.ts</c>. The port's <see cref="ParsedKey"/> has <c>Modifiers</c>, <c>Name</c>, and
/// <c>Character</c> where upstream's key is a discriminated <c>{ kind, name | char }</c>, so each assertion checks the same fields spelled in C#.
/// </summary>
public sealed class ParseKeyTests
{
    [Fact]
    public void Rejects_non_printable_single_code_points_control_format_surrogate_separators()
    {
        // Code units are built from their numbers: a raw line separator or a lone surrogate in source would not survive the toolchain.
        foreach (var key in new[] { Unit(0x0A), Unit(0x00), Unit(0x200B), Unit(0xD800), Unit(0x2028), Unit(0x7F), "Shift+" + Unit(0x09) })
        {
            Assert.Null(KeyGrammar.ParseKey(key));
        }

        AssertKey(KeyGrammar.ParseKey(Unit(0xE9)), [], null, Unit(0xE9));
    }

    [Fact]
    public void Parses_named_keys_single_characters_and_modifier_chords()
    {
        AssertKey(KeyGrammar.ParseKey("Enter"), [], "Enter", null);
        AssertKey(KeyGrammar.ParseKey("a"), [], null, "a");
        AssertKey(KeyGrammar.ParseKey("$"), [], null, "$");
        AssertKey(KeyGrammar.ParseKey("Control+a"), ["Control"], null, "a");
        AssertKey(KeyGrammar.ParseKey("Shift+Tab"), ["Shift"], "Tab", null);
        AssertKey(KeyGrammar.ParseKey("ControlOrMeta+Shift+ArrowLeft"), ["ControlOrMeta", "Shift"], "ArrowLeft", null);
    }

    [Fact]
    public void Treats_a_trailing_plus_as_the_plus_character()
    {
        AssertKey(KeyGrammar.ParseKey("+"), [], null, "+");
        AssertKey(KeyGrammar.ParseKey("Shift++"), ["Shift"], null, "+");
    }

    [Fact]
    public void Rejects_unknown_names_misspelt_or_repeated_modifiers_bare_modifiers_and_several_characters()
    {
        foreach (var key in new[] { "", "Ctrl+a", "Enter+Shift", "Shift", "Shift+Shift+a", "ab", "enter", "Shift+", "Control+Shift" })
        {
            Assert.Null(KeyGrammar.ParseKey(key));
        }
    }

    private static string Unit(int code) => ((char)code).ToString();

    private static void AssertKey(ParsedKey? parsed, string[] modifiers, string? name, string? character)
    {
        Assert.NotNull(parsed);
        Assert.Equal(modifiers, parsed.Modifiers);
        Assert.Equal(name, parsed.Name);
        Assert.Equal(character, parsed.Character);
    }
}
