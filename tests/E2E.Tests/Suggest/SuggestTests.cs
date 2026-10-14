// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Suggest;

public sealed class SuggestTests
{
    private static readonly string[] Keys = ["targets", "tests", "timeout", "retries", "workers", "reporters", "agent", "cache"];

    [Fact]
    public void Finds_the_candidate_one_typo_away()
    {
        Assert.Equal("targets", Suggestions.Suggest("target", Keys));
        Assert.Equal("retries", Suggestions.Suggest("retires", Keys));
        Assert.Equal("reporters", Suggestions.Suggest("reporter", Keys));
        Assert.Equal("timeout", Suggestions.Suggest("Timeout", Keys));
    }

    [Fact]
    public void Allows_more_edits_for_longer_words_and_none_beyond_a_third_of_the_length()
    {
        Assert.Equal("reporters", Suggestions.Suggest("reprters", Keys));
        Assert.Equal(
            "tests/example.e2e.ts",
            Suggestions.Suggest("tests/exmple.e2e.ts", ["tests/example.e2e.ts", "tests/other.e2e.ts"]));
        Assert.Null(Suggestions.Suggest("xyz", Keys));
        Assert.Null(Suggestions.Suggest("completelydifferent", Keys));
    }

    [Fact]
    public void Returns_nothing_for_an_exact_match_or_an_empty_candidate_list()
    {
        Assert.Null(Suggestions.Suggest("targets", Keys));
        Assert.Null(Suggestions.Suggest("targets", []));
    }

    [Fact]
    public void Judges_one_pair_by_the_same_budget_ignoring_case()
    {
        Assert.True(Suggestions.IsTypoOf("Note", "notes"));
        Assert.True(Suggestions.IsTypoOf("sumbit", "submit"));
        Assert.False(Suggestions.IsTypoOf("note", "nothing"));
        Assert.False(Suggestions.IsTypoOf("the", "themes"));
    }
}
