// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using E2E.Internal;

namespace E2E.Tests.SecretLedger;

public sealed class StreamRedactorTests
{
    private const string Secret = "synthetic-stream-secret-2718";

    private static Redactor Registered() => Ledger.Of(("token", Secret));

    [Fact]
    public void Redacts_a_value_split_across_two_writes()
    {
        var stream = new StreamRedactor(Registered());
        var first = stream.Push("token synthetic-stream-");
        var second = stream.Push("secret-2718 leaked\n");
        Assert.DoesNotContain("synthetic", first);
        Assert.Equal("token <secret:token> leaked\n", first + second);
    }

    [Fact]
    public void Passes_a_finished_line_through_at_once_and_holds_an_unfinished_one_for_the_next_write()
    {
        var stream = new StreamRedactor(Registered());
        Assert.Equal("hello\n", stream.Push("hello\n"));
        Assert.Equal("", stream.Push("progress"));
        Assert.Equal("progress 50%\n", stream.Push(" 50%\n"));
    }

    [Fact]
    public void Holds_no_more_of_a_long_unfinished_line_than_the_longest_spelling_less_one_character()
    {
        var registered = Registered();
        var stream = new StreamRedactor(registered);
        var held = registered.MaxFormLength - 1;
        Assert.Equal(new string('x', 200 - held), stream.Push(new string('x', 200)));
        Assert.Equal(new string('x', held), stream.Flush());
    }

    [Fact]
    public void Moves_the_cut_back_over_an_occurrence_it_would_split()
    {
        var stream = new StreamRedactor(Registered());
        Assert.Equal("prefix ", stream.Push($"prefix {Secret}{new string('y', 30)}"));
        Assert.Equal($"<secret:token>{new string('y', 30)}", stream.Flush());
    }

    [Fact]
    public void Holds_nothing_when_no_value_is_registered()
    {
        var stream = new StreamRedactor(Ledger.Of());
        Assert.Equal("partial", stream.Push("partial"));
        Assert.Equal("", stream.Flush());
    }

    [Fact]
    public void Redacts_a_run_of_overlapping_occurrences_longer_than_any_buffer_as_the_whole_would_holding_under_two_spellings()
    {
        var periodic = Ledger.Of(("s", "abababab"));
        var stream = new StreamRedactor(periodic);
        var text = string.Concat(Enumerable.Repeat("ab", 40_000));
        var output = new StringBuilder();
        for (var at = 0; at < text.Length; at += 1_001)
        {
            output.Append(stream.Push(text.Substring(at, Math.Min(1_001, text.Length - at))));
        }

        Assert.DoesNotContain("abababab", output.ToString());
        var tail = stream.Flush();
        Assert.True(tail.Replace("<secret:s>", "abababab").Length < 2 * periodic.MaxFormLength);
        Assert.Equal(periodic.Redact(text), output + tail);
        Assert.Equal(string.Concat(Enumerable.Repeat("<secret:s>", 10_000)), output + tail);
    }

    [Fact]
    public void Decodes_bytes_as_UTF_8_so_a_character_split_across_two_writes_still_completes_a_value()
    {
        var euro = Ledger.Of(("pay", "pay€2718secret"));
        var stream = new StreamRedactor(euro);
        var bytes = Encoding.UTF8.GetBytes("token pay€2718secret leaked\n");
        var mid = Encoding.UTF8.GetByteCount("token pay") + 1;
        var first = stream.Push(bytes.AsSpan(0, mid));
        var second = stream.Push(bytes.AsSpan(mid));
        Assert.DoesNotContain("�", first);
        Assert.Equal("token <secret:pay> leaked\n", first + second);
    }

    [Fact]
    public void Releases_an_unfinished_character_on_flush_instead_of_holding_it()
    {
        var stream = new StreamRedactor(Registered());
        Assert.Equal("hi�", stream.Push(new byte[] { 0x68, 0x69, 0xe2 }) + stream.Flush());
    }
}
