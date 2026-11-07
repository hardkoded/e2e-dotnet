// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.CliCache;

/// <summary><c>describe('e2e cache ls')</c> of upstream <c>tests/unit/cli-cache.test.ts</c>.</summary>
public sealed class E2eCacheLsTests : IDisposable
{
    private readonly CacheCli _cli = new();

    public void Dispose() => _cli.Dispose();

    private static string Digest(string instruction) => Identity.InstructionDigest(instruction)[..12];

    private string[] Lines() => [.. _cli.Stdout.TrimEnd().Split('\n').Select(line => line.TrimEnd('\r'))];

    [Fact]
    public void Prints_the_provenance_age_and_action_count_of_every_entry()
    {
        _cli.Seed(
            (new string('a', 64), CacheCli.Trace("tests/signup.e2e.ts::signs up", "web", "sign up", 3)),
            (new string('b', 64), CacheCli.Trace("tests/billing.e2e.ts::upgrades", "safari", "upgrade", 1)));
        _cli.Invoke("ls");
        var lines = Lines();
        Assert.Equal(0, _cli.ExitCode);
        Assert.Matches(@"^TEST +TARGET +INSTRUCTION +AGE +ACTIONS\z", lines[0]);
        // Sorted by test, so billing precedes signup whatever the file order was.
        Assert.Matches(@"^tests/billing\.e2e\.ts::upgrades +safari +" + Digest("upgrade") + @" +<1m +1\z", lines[1]);
        Assert.Matches(@"^tests/signup\.e2e\.ts::signs up +web +" + Digest("sign up") + @" +<1m +3\z", lines[2]);
    }

    [Fact]
    public void Marks_a_recording_the_action_cap_cut_short()
    {
        var entry = CacheCli.Trace(null, null, null, 2);
        entry.Truncated = true;
        _cli.Seed((new string('b', 64), entry));
        _cli.Invoke("ls");
        Assert.Matches(@" 2 \(truncated\)\z", Lines()[1]);
    }

    [Theory]
    [InlineData("tests/中文.e2e.ts::passes", 2, 5)]
    [InlineData("tests/é.e2e.ts::passes", 2, 2)]
    [InlineData("tests/👩‍💻.e2e.ts::passes", 2, 3)]
    public void Aligns_columns_by_displayed_width(string testId, int padding, int asciiPadding)
    {
        const string asciiId = "tests/a.e2e.ts::passes";
        _cli.Seed(
            (new string('a', 64), CacheCli.Trace(asciiId, "web", "first", 1)),
            (new string('b', 64), CacheCli.Trace(testId, "web", "second", 1)));
        _cli.Invoke("ls");
        var lines = Lines();
        Assert.Equal(0, _cli.ExitCode);
        Assert.Equal(3, lines.Length);
        Assert.Equal(asciiId + new string(' ', asciiPadding) + "web     " + Digest("first") + "  <1m  1", lines.Single(line => line.StartsWith(asciiId, StringComparison.Ordinal)));
        Assert.Equal(testId + new string(' ', padding) + "web     " + Digest("second") + "  <1m  1", lines.Single(line => line.StartsWith(testId, StringComparison.Ordinal)));
    }

    [Fact]
    public void Reports_an_empty_store_instead_of_failing()
    {
        _cli.Invoke("ls");
        Assert.Contains("no replay cache entries in", _cli.Stdout, StringComparison.Ordinal);
        Assert.Equal(0, _cli.ExitCode);
    }

    [Fact]
    public void Counts_a_file_the_store_cannot_read_as_unreadable_not_as_an_entry()
    {
        Directory.CreateDirectory(_cli.Store);
        File.WriteAllText(Path.Combine(_cli.Store, new string('c', 64) + ".json"), "{ not json");
        _cli.Invoke("ls");
        Assert.Contains("no replay cache entries in", _cli.Stdout, StringComparison.Ordinal);
        Assert.Contains("are not readable trace-1 entries", _cli.Stderr, StringComparison.Ordinal);
    }
}
