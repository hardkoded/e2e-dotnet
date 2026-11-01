// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.RunSelection;

/// <summary>A fixture for discovery: its test never runs, so no engine starts.</summary>
[Category("Checkout")]
public sealed class DiscoveredFixture : E2ETest
{
    [Test]
    [Category("Slow")]
    [Ignore("only found by reflection")]
    public void Is_found_by_reflection()
    {
    }
}

/// <summary>The test a nested run starts, which the filters left out.</summary>
[Explicit("Run by RunSelectionTests")]
public sealed class LeftOutFixture : E2ETest
{
    [Test]
    public void Is_left_out() => Assert.Fail("A test the filters left out must not run.");

    public static bool DerivedSetUpRan;

    // NUnit runs a base [SetUp] first, so a left-out test ends before this.
    [SetUp]
    public void RecordDerivedSetUp() => DerivedSetUpRan = true;
}

/// <summary>
/// .NET-only (upstream collects tests by running test files): finding the tests of an assembly by reflection, and the test that skips itself
/// when the filters left it out.
/// </summary>
public sealed class RunSelectionTests
{
    [Test]
    public void Finds_the_tests_of_the_assembly_with_their_categories_and_skip_reason()
    {
        var found = E2E.NUnit.RunSelection.Discover(typeof(RunSelectionTests).Assembly).Single(test => test.Key == $"{typeof(DiscoveredFixture).FullName}.{nameof(DiscoveredFixture.Is_found_by_reflection)}");

        Assert.That(found.Namespace, Is.EqualTo("E2E.NUnit.Tests.RunSelection"));
        Assert.That(found.TypeName, Is.EqualTo(nameof(DiscoveredFixture)));
        Assert.That(found.Tags, Is.EquivalentTo(new[] { "Checkout", "Slow" }));
        Assert.That(found.SkipReason, Is.EqualTo("only found by reflection"));
    }

    [Test]
    public void Skips_an_explicit_fixture_when_looking_for_tests()
    {
        var found = E2E.NUnit.RunSelection.Discover(typeof(RunSelectionTests).Assembly);

        Assert.That(found.Select(test => test.TypeName), Does.Not.Contain(nameof(LeftOutFixture)));
    }

    [Test]
    public void Keeps_what_one_run_fixture_left_out_when_another_adds_its_own()
    {
        E2E.NUnit.RunSelection.Set(new Dictionary<string, string> { [$"{typeof(LeftOutFixture).FullName}.A"] = "one" });
        try
        {
            E2E.NUnit.RunSelection.Add(new Dictionary<string, string> { [$"{typeof(LeftOutFixture).FullName}.B"] = "two" });

            Assert.That(E2E.NUnit.RunSelection.ReasonFor(typeof(LeftOutFixture), "A"), Is.EqualTo("one"));
            Assert.That(E2E.NUnit.RunSelection.ReasonFor(typeof(LeftOutFixture), "B"), Is.EqualTo("two"));
        }
        finally
        {
            E2E.NUnit.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    [Test]
    public void Ignores_a_test_the_filters_left_out_with_their_reason_and_never_starts_it()
    {
        E2E.NUnit.RunSelection.Set(new Dictionary<string, string> { [$"{typeof(LeftOutFixture).FullName}.{nameof(LeftOutFixture.Is_left_out)}"] = "login.cs did not change" });
        try
        {
            var result = NestedRun.Run(nameof(LeftOutFixture.Is_left_out));

            Assert.That(result.ResultState.Status, Is.EqualTo(TestStatus.Skipped));
            Assert.That(result.Message, Does.EndWith("login.cs did not change"));
            Assert.That(LeftOutFixture.DerivedSetUpRan, Is.False);
        }
        finally
        {
            E2E.NUnit.RunSelection.Set(new Dictionary<string, string>());
        }
    }

    private sealed class LeaveEverythingOut(string root) : E2ERunFixture
    {
        private sealed class All : ITestFilter
        {
            public string Name => "all";

            public Task<TestFilterResult> FilterAsync(IReadOnlyList<TestFilterCandidate> tests, TestFilterContext context) =>
                Task.FromResult(new TestFilterResult { Exclude = tests.ToDictionary(test => test.Id, _ => "left out") });
        }

        protected override IReadOnlyList<ITestFilter> Filters => [new All()];

        protected override string? ChangedSince => "main";

        protected override E2EConfig Config => E2EConfig.Parse("{}", root, _ => null);
    }

    [Test]
    public async Task Never_filters_a_run_that_opens_a_session_on_a_named_test()
    {
        var dir = FilteredRealModelTests.FeatureRepo();
        try
        {
            var fixture = new LeaveEverythingOut(dir);
            var key = typeof(ControlInventory.Agent.ControlInventoryTests);

            // The control: without a session request the filter leaves the test out.
            await fixture.SelectTestsAsync();
            Assert.That(E2E.NUnit.RunSelection.ReasonFor(key, nameof(ControlInventory.Agent.ControlInventoryTests.Check_picks_a_radio_the_pick_replaces_with_its_summary)), Is.EqualTo("left out"));

            E2E.NUnit.RunSelection.Set(new Dictionary<string, string>());
            using (SessionHandover.Begin("any test"))
            {
                await fixture.SelectTestsAsync();
            }

            Assert.That(E2E.NUnit.RunSelection.ReasonFor(key, nameof(ControlInventory.Agent.ControlInventoryTests.Check_picks_a_radio_the_pick_replaces_with_its_summary)), Is.Null);
        }
        finally
        {
            E2E.NUnit.RunSelection.Set(new Dictionary<string, string>());
            FilteredRealModelTests.RemoveRepo(dir);
        }
    }
}
