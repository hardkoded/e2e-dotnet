// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.AgentPin;

/// <summary>
/// .NET-only: runs <see cref="PinnedFixture"/> through NUnit to check that the adapter hands the test method and class to
/// the session. A pin naming no configured agent fails the session start, so the result shows which pin was read.
/// </summary>
public sealed class AnAgentPinReachesTheSessionTests
{
    [Test]
    public void Reads_the_pin_of_the_test_method()
    {
        var result = NestedRun.Run(nameof(PinnedFixture.Method_pinned_to_a_missing_agent));

        Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(result.Message, Does.Contain("unknown agent \"method-pin\""));
    }

    [Test]
    public void Reads_the_pin_of_the_test_class()
    {
        var result = NestedRun.Run(nameof(PinnedFixture.Method_without_a_pin));

        Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(result.Message, Does.Contain("unknown agent \"class-pin\""));
    }
}

[Explicit("Run by AnAgentPinReachesTheSessionTests")]
[Agent("class-pin")]
public sealed class PinnedFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    [Test]
    [Agent("method-pin")]
    public void Method_pinned_to_a_missing_agent()
    {
    }

    [Test]
    public void Method_without_a_pin()
    {
    }
}
