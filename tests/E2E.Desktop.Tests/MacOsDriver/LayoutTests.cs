// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using E2E.Desktop.MacOs;

namespace E2E.Desktop.Tests.MacOsDriver;

/// <summary>Ported from the <c>tests</c> module of <c>crates/e2e-desktop-napi/src/layout.rs</c>.</summary>
public sealed class LayoutTests
{
    private static (ushort, Rune)[] Table(params (ushort, char)[] entries) =>
        [.. entries.Select(entry => (entry.Item1, new Rune(entry.Item2)))];

    [Fact]
    public void Finds_the_key_that_types_a_character_on_the_layout()
    {
        // French AZERTY: the keys a US keyboard has for a and q are swapped, as are z and w.
        var french = Table((0, 'q'), (12, 'a'), (6, 'w'), (13, 'z'));
        Assert.Equal((ushort)12, Layout.CodeFor(new Rune('a'), french));
        Assert.Equal((ushort)0, Layout.CodeFor(new Rune('q'), french));
        Assert.Equal((ushort)13, Layout.CodeFor(new Rune('z'), french));
        Assert.Null(Layout.CodeFor(new Rune('k'), french));

        // AZERTY types é on the 2 key without Shift; only the keypad types 2, and that is no shortcut key.
        Assert.Null(Layout.CodeFor(new Rune('2'), Table((19, 'é'), (84, '2'))));
        Assert.Equal((ushort)19, Layout.CodeFor(new Rune('2'), Table((19, '2'), (84, '2'))));
    }
}
