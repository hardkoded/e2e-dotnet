// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.ParallelChildren;

/// <summary>
/// No upstream <c>it</c>: NUnit shares one fixture instance among its tests, so the adapter refuses
/// a fixture whose tests run in parallel on it.
/// </summary>
public sealed class ParallelChildrenTests
{
    [Test]
    public void Fails_a_test_whose_fixture_runs_its_children_in_parallel()
    {
        var result = NestedRun.Run(nameof(ParallelChildrenFixture.Runs_in_parallel));

        Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(result.Message, Does.Contain("in parallel"));
    }
}

[Explicit("Run by ParallelChildrenTests")]
[Parallelizable(ParallelScope.Children)]
public sealed class ParallelChildrenFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    [Test]
    public void Runs_in_parallel()
    {
    }
}
