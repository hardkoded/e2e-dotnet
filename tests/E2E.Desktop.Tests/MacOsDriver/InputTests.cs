// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using E2E.Desktop.MacOs;

namespace E2E.Desktop.Tests.MacOsDriver;

/// <summary>Ported from the <c>tests</c> module of <c>crates/e2e-desktop-napi/src/input.rs</c>.</summary>
public sealed class InputTests
{
    private const ulong FlagShift = 0x0002_0000;

    private static (ushort, Rune)[] Table(params (ushort, char)[] entries) =>
        [.. entries.Select(entry => (entry.Item1, new Rune(entry.Item2)))];

    [Fact]
    public void Hands_out_input_ids_no_two_threads_share()
    {
        var threads = Enumerable
            .Range(0, 4)
            .Select(_ => Task.Run(() => Enumerable.Range(0, 1000).Select(_ => Input.NextInputId()).ToList()))
            .ToList();
        var ids = threads.SelectMany(thread => thread.Result).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(0u, ids);
    }

    [Fact]
    public void Gives_a_typed_character_the_key_that_types_it_on_the_layout()
    {
        // A US layout's a and b keys, and their Shift layer; no key types an emoji.
        var us = Table((0, 'a'), (11, 'b'));
        var usShifted = Table((0, 'A'), (11, 'B'));
        Assert.Equal(((ushort)11, false), Input.KeyCode("b", us, usShifted));
        Assert.Equal(((ushort)0, false), Input.KeyCode("a", us, usShifted));
        Assert.Equal(((ushort)11, true), Input.KeyCode("B", us, usShifted));
        Assert.Equal(((ushort)0, false), Input.KeyCode("😀", us, usShifted));

        // French AZERTY types a on the key a US keyboard has for q.
        Assert.Equal(((ushort)12, false), Input.KeyCode("a", Table((12, 'a'), (0, 'q')), []));
    }

    [Fact]
    public void Sends_one_character_per_key_event_and_keeps_a_surrogate_pair_in_one()
    {
        var typed = Input.Pieces("12😀x");
        Assert.Equal(["1", "2", "😀", "x"], typed.Select(piece => Assert.IsType<Input.Piece.Text>(piece).Units));
        Assert.Equal(2, ((Input.Piece.Text)typed[2]).Units.Length);
    }

    [Fact]
    public void Presses_return_once_for_a_windows_line_break()
    {
        Assert.Equal(
            new Input.Piece[]
            {
                new Input.Piece.Text("a"),
                new Input.Piece.Return(),
                new Input.Piece.Text("b"),
                new Input.Piece.Return(),
                new Input.Piece.Text("c"),
                new Input.Piece.Return(),
                new Input.Piece.Text("d"),
                new Input.Piece.Tab(),
                new Input.Piece.Text("e"),
            },
            Input.Pieces("a\r\nb\nc\rd\te")
        );
    }

    [Fact]
    public void Types_the_shifted_character_of_a_shift_combo()
    {
        // No Command, Control, or Option combo: those read the machine's keyboard layout, which a test cannot count on.
        foreach (var (combo, typed) in new[] { ("shift+a", 'A'), ("A", 'A'), ("shift+1", '!'), ("a", 'a') })
        {
            var parsed = Input.ParseCombo(combo);
            Assert.Equal(new Rune(typed), parsed.Key.Character);
        }

        Assert.True(Input.ParseCombo("shift+a").HasShift);
        Assert.Equal(FlagShift, Input.ParseCombo("shift+a").Flags);
    }

    [Fact]
    public void Keeps_a_cancel_that_came_before_its_run_while_an_older_run_starts()
    {
        Input.CancelInput(1_000_021);
        using var older = Input.TypingRun.Start(1_000_020);
        using var cancelled = Input.TypingRun.Start(1_000_021);
        Assert.False(older.Cancelled());
        Assert.True(cancelled.Cancelled());
    }

    [MacOnlyFact]
    public void Says_nothing_was_sent_when_typing_stops_before_its_first_key()
    {
        var later = Instant.After(TimeSpan.FromSeconds(60));

        // A pid no process has: nothing below may reach it.
        var route = Route.Process(-1);
        using var first = Input.TypingRun.Start(null);
        var lost = Assert.Throws<Failure>(() =>
            Input.TypeText("ab", route, first, later, () =>
                throw Failure.Refused(Codes.ActionFailed, "lost the keyboard")
            )
        );
        Assert.Equal(Delivery.NotDelivered, lost.Delivery);
        Input.CancelInput(1_000_041);
        using var run = Input.TypingRun.Start(1_000_041);
        var cancelled = Assert.Throws<Failure>(() => Input.TypeText("ab", route, run, later, () => { }));
        Assert.Equal(Delivery.NotDelivered, cancelled.Delivery);
    }

    [Fact]
    public void Refuses_command_q_however_it_is_written()
    {
        foreach (var combo in new[] { "cmd+q", "command+Q", "meta+q" })
        {
            Assert.Throws<Failure>(() => Input.ParseCombo(combo));
        }

        // Control-Q is no quit; whether it resolves depends on the layout this Mac has. Resolving a key needs the macOS keyboard layout.
        if (OperatingSystem.IsMacOS())
        {
            try
            {
                Input.ParseCombo("ctrl+q");
            }
            catch (Failure failure)
            {
                Assert.NotEqual(Codes.PolicyDenied, failure.Code);
            }
        }
    }
}
