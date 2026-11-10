// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AgentPrompts;

/// <summary>
/// What a wait compares of a judged location (upstream's <c>judgmentShape</c> tests in <c>agent-prompts.test.ts</c>). The port's
/// <c>JudgmentShape</c> decides whether the address is left out of the comparison: a tree the call withholds is not shown it. That
/// part is covered end to end by the vision-only case in <c>AgentJudgmentTierTests</c>.
/// </summary>
public sealed class JudgmentShapeTests
{
    private const string App = "https://app.test";

    private static string ShapeAt(string location) => E2E.AgentObservation.JudgedLocationShape(E2E.AgentObservation.JudgeLocation(location, App));

    [Fact]
    public void Partial_ignores_a_clock_in_a_device_title_as_the_screen_shape_does()
    {
        Assert.Equal(ShapeAt("com.example / Recording 00:12"), ShapeAt("com.example / Recording 00:13"));
    }

    [Fact]
    public void Partial_ignores_a_clock_in_a_title_that_parses_as_a_URL_with_another_scheme()
    {
        Assert.Equal(ShapeAt("Timer: 00:12"), ShapeAt("Timer: 00:13"));
    }

    [Fact]
    public void Partial_tells_two_addresses_apart()
    {
        Assert.NotEqual(ShapeAt("https://app.test/a"), ShapeAt("https://app.test/b"));
    }

    [Fact]
    public void Partial_sees_a_time_change_in_a_URL_because_it_names_another_page()
    {
        Assert.NotEqual(ShapeAt("https://app.test/at/10:15"), ShapeAt("https://app.test/at/10:16"));
    }

    [Fact]
    public void Partial_sees_a_change_in_the_query()
    {
        Assert.NotEqual(ShapeAt("https://app.test/a?page=1"), ShapeAt("https://app.test/a?page=2"));
    }
}
