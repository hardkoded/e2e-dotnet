// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.Select.SelectKit;

namespace E2E.Tests.Select;

/// <summary>
/// The fixme <c>it</c>s of upstream's <c>describe('select')</c> (<c>tests/unit/select.test.ts</c>). The collection is the host's discovery
/// (<c>HostTestFilters.Collect</c>) of tests as the adapters find them, where upstream collects <c>test()</c> calls with <c>collectModule</c>:
/// <c>test.fixme(title, fn)</c> is a test marked <c>[Fixme]</c>, a skip option is <c>[Ignore]</c> (a <c>SkipReason</c>).
/// </summary>
public sealed class SelectTests
{
    private static DiscoveredTest Found(string title, string? skipReason = null, bool fixme = false) =>
        new(title, "E2E.Tests.Select", "Fixtures", title, [], skipReason, fixme);

    private static Collection Collected(params DiscoveredTest[] tests) => HostTestFilters.Collect(tests, [], [], out _);

    [Fact]
    public void Gives_a_test_fixme_registration_the_cause_fixme_and_a_skip_option_reading_fixme_the_cause_explicit()
    {
        var col = Collected(
            Found("runs"),
            Found("registered", "fixme", fixme: true),
            Found("by option", "fixme"),
            Found("inside a skipped group", "fixme", fixme: true),
            Found("plain member", "tracked in #42")
        );
        var skips = Selector
            .Select(col, ConfigOf(), new SelectionFilters())
            .Pairs.Select(pair => (pair.Test.Title, pair.Disposition, pair.Skip))
            .ToArray();
        Assert.Equal(
            [
                ("runs", Disposition.Run, null),
                ("registered", Disposition.Skip, new SkipInfo("fixme", "fixme")),
                ("by option", Disposition.Skip, new SkipInfo("explicit", "fixme")),
                ("inside a skipped group", Disposition.Skip, new SkipInfo("fixme", "fixme")),
                ("plain member", Disposition.Skip, new SkipInfo("explicit", "tracked in #42")),
            ],
            skips
        );
    }

    [Fact]
    public void Errors_on_zero_runnable_ordinary_pairs_unless_passWithNoTests()
    {
        var col = Collected(Found("skipped", "skipped"));
        var error = Assert.Throws<ConfigurationException>(() =>
            Selector.Select(col, ConfigOf(), new SelectionFilters())
        );
        Assert.Equal(
            "1 tests were collected but none is runnable: 1 skipped explicitly; pass --pass-with-no-tests to allow this",
            error.Message
        );
        Selector.Select(col, ConfigOf(), new SelectionFilters(), passWithNoTests: true);
        var fixme = Collected(Found("not yet", "fixme", fixme: true));
        error = Assert.Throws<ConfigurationException>(() => Selector.Select(fixme, ConfigOf(), new SelectionFilters()));
        Assert.Equal(
            "1 tests were collected but none is runnable: 1 marked fixme; pass --pass-with-no-tests to allow this",
            error.Message
        );
    }
}
