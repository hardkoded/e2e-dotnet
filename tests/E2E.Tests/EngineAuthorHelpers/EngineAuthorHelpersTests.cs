// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.EngineAuthorHelpers;

/// <summary>
/// The shared engine semantics an engine outside the assembly imports instead of carrying a copy (upstream's <c>e2e/engine</c> exports):
/// <c>resolveExpression</c>, <c>sameSite</c>, <c>urlMatches</c>, <c>matchesText</c>, <c>validateJsonValue</c>, <c>isSecret</c>,
/// <c>raceAbort</c>, <c>withTimeout</c>, and <c>withinCleanupBudget</c>.
/// </summary>
public sealed class EngineAuthorHelpersTests
{
    [Fact]
    public void Lists_the_modifiers_and_named_keys_of_the_grammar()
    {
        Assert.Contains("ControlOrMeta", KeyGrammar.KeyModifiers);
        Assert.Contains("ArrowUp", KeyGrammar.KeyNames);
    }

    [Fact]
    public void Resolves_an_expression_over_the_tree_it_is_given()
    {
        var tree = new[]
        {
            new SemanticNode { Ref = "save", Role = "button", Name = "Save" },
            new SemanticNode { Ref = "cancel", Role = "button", Name = "Cancel" },
        };

        var matches = ExpressionResolution.ResolveExpression(LocatorExpression.ForRole("button", null, new RoleOptions()), tree);

        Assert.Equal(["save", "cancel"], matches.Select(node => node.Ref));
    }

    [Fact]
    public void Answers_a_selector_only_through_the_selector_it_is_given()
    {
        var tree = new[] { new SemanticNode { Ref = "a", Role = "button", Name = "A" } };
        var selector = new LocatorExpression { Kind = "selector", Selector = ".primary" };

        Assert.Throws<EngineException>(() => ExpressionResolution.ResolveExpression(selector, tree));
        var answered = ExpressionResolution.ResolveExpression(
            selector,
            tree,
            new ResolveExpressionOptions { Selector = (_, candidates) => candidates });
        Assert.Equal(["a"], answered.Select(node => node.Ref));
    }

    [Theory]
    [InlineData("https://app.example.com/a", "example.com", true)]
    [InlineData("https://shop.example.co.uk/", "example.co.uk", true)]
    [InlineData("https://evil.com/", "example.com", false)]
    [InlineData("not a url", "example.com", false)]
    public void Tells_whether_a_url_is_on_a_site(string url, string site, bool expected) =>
        Assert.Equal(expected, E2E.Internal.Urls.SameSite(url, site));

    [Fact]
    public void Reads_a_string_url_pattern_against_the_base_and_folds_case_on_request()
    {
        Assert.True(E2E.Internal.Urls.UrlMatches("https://app.example.com/home", "/home", "https://app.example.com/"));
        Assert.False(E2E.Internal.Urls.UrlMatches("https://app.example.com/Home", "/home", "https://app.example.com/"));
        Assert.True(E2E.Internal.Urls.UrlMatches("https://app.example.com/Home", "/home", "https://app.example.com/", ignoreCase: true));
    }

    [Fact]
    public void Tests_a_regular_expression_against_the_whole_url_and_applies_ignore_case()
    {
        Assert.True(E2E.Internal.Urls.UrlMatches("https://app.example.com/home", new Regex("/HOME$"), ignoreCase: true));
        Assert.False(E2E.Internal.Urls.UrlMatches("https://app.example.com/home", new Regex("/HOME$"), ignoreCase: false));
    }

    [Fact]
    public void Matches_text_exactly_or_as_a_case_insensitive_substring()
    {
        Assert.True(TextRules.MatchesText("  Save   draft ", TextMatch.FromString("Save draft")));
        Assert.False(TextRules.MatchesText("Save draft now", TextMatch.FromString("Save draft")));
        Assert.True(TextRules.MatchesText("Save draft now", TextMatch.FromString("save DRAFT"), exact: false));
    }

    [Fact]
    public void Accepts_plain_json_and_refuses_what_is_not_plain_json()
    {
        JsonValueValidation.ValidateJsonValue(new Dictionary<string, object?> { ["n"] = 1, ["list"] = new List<object?> { "x", true, null } }, "params");

        var nan = Assert.Throws<TestException>(() => JsonValueValidation.ValidateJsonValue(double.NaN, "params"));
        Assert.Equal("INVALID_ARGUMENT", nan.Code);
        Assert.Throws<TestException>(() => JsonValueValidation.ValidateJsonValue(new object(), "params"));

        var cycle = new List<object?>();
        cycle.Add(cycle);
        Assert.Throws<TestException>(() => JsonValueValidation.ValidateJsonValue(cycle, "params"));
    }

    [Fact]
    public void Refuses_a_secret_in_json_with_the_policy_code_unless_a_rule_says_otherwise()
    {
        var secret = Secret.Create("token", "s3cret-value", "generic-secret");
        var denied = Assert.Throws<ConfigurationException>(() => JsonValueValidation.ValidateJsonValue(new Dictionary<string, object?> { ["t"] = secret }, "params"));
        Assert.Equal("POLICY_DENIED", denied.Code);

        var ruled = Assert.Throws<TestException>(() => JsonValueValidation.ValidateJsonValue(
            new List<object?> { secret },
            "params",
            new JsonValueRules { OnSecret = label => throw new TestException("INVALID_ARGUMENT", label + " is a secret") }));
        Assert.Equal("params is a secret", ruled.Message);
    }

    [Fact]
    public void Tells_a_secret_handle_from_other_values()
    {
        var secret = Secret.Create("token", "s3cret-value", "generic-secret");
        Assert.True(Secret.IsSecret(secret));
        Assert.False(Secret.IsSecret("s3cret-value"));
    }

    [Fact]
    public async Task Abandons_a_cancelled_action_as_cancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var error = await Assert.ThrowsAsync<EngineException>(() => Timing.RaceAbortAsync(() => Task.FromResult(1), cancelled.Token, "press"));
        Assert.Equal("CANCELLED", error.Code);
    }

    [Fact]
    public async Task Fails_a_slow_step_at_its_timeout_with_the_error_it_builds()
    {
        var slow = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(static _ => 1, TaskScheduler.Default);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => E2E.Internal.Time.WithTimeout(slow, TimeSpan.FromMilliseconds(20), () => new InvalidOperationException("timed out")));
        Assert.Equal("timed out", error.Message);
    }

    [Fact]
    public async Task Ends_a_cleanup_at_its_budget_and_absorbs_its_failure()
    {
        var never = new TaskCompletionSource();
        await E2E.Internal.Time.WithinCleanupBudget(never.Task, CancellationToken.None, TimeSpan.FromMilliseconds(20));

        var failed = Task.FromException(new InvalidOperationException("close failed"));
        await E2E.Internal.Time.WithinCleanupBudget(failed, CancellationToken.None, TimeSpan.FromSeconds(5));
    }
}
