// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Execution;

namespace E2E.NUnit.Tests.RelevanceOrder;

/// <summary>Two tests whose order the relevance scores change.</summary>
[Explicit("Only ordered by RelevanceOrderTests")]
public sealed class RelevanceFixture : E2ETest
{
    [Test]
    public void Alpha() { }

    [Test]
    public void Beta() { }

    [Test]
    public void Gamma() { }
}

/// <summary>
/// .NET-only (upstream orders its work plans in the runner, which the port has not; NUnit builds its test tree before the run fixture runs):
/// the scores the filters gave reorder the tests of the tree, likeliest first, and keep NUnit's order for tests of one band.
/// </summary>
public sealed class RelevanceOrderTests
{
    [Test]
    public void Orders_the_children_of_a_work_item_by_relevance_band()
    {
        var key = typeof(RelevanceFixture).FullName + ".";
        E2E.NUnit.RunSelection.SetRelevance(
            new Dictionary<string, double> { [key + "Gamma"] = 0.9, [key + "Beta"] = 0.3 }
        );
        try
        {
            var fixture = BuildItem();
            E2E.NUnit.RunSelection.OrderByRelevance(fixture);

            Assert.That(
                fixture.Children.Select(child => child.Test.Name),
                Is.EqualTo(new[] { "Gamma", "Beta", "Alpha" })
            );
        }
        finally
        {
            E2E.NUnit.RunSelection.SetRelevance(new Dictionary<string, double>());
        }
    }

    [Test]
    public void Keeps_the_order_NUnit_gave_when_no_test_is_scored()
    {
        var fixture = BuildItem();
        var before = fixture.Children.Select(child => child.Test.Name).ToList();
        E2E.NUnit.RunSelection.OrderByRelevance(fixture);

        Assert.That(fixture.Children.Select(child => child.Test.Name), Is.EqualTo(before));
    }

    // The tree NUnit built for this assembly, down to the fixture; an explicit fixture is still in it.
    private static CompositeWorkItem BuildItem()
    {
        var assembly = new DefaultTestAssemblyBuilder().Build(
            typeof(RelevanceFixture).Assembly,
            new Dictionary<string, object>()
        );
        var suite = Find(assembly, typeof(RelevanceFixture).FullName!)!;
        return (CompositeWorkItem)WorkItemBuilder.CreateWorkItem(suite, TestFilter.Empty, true)!;
    }

    private static TestSuite? Find(ITest test, string fullName)
    {
        if (test is TestSuite suite && test.FullName == fullName)
        {
            return suite;
        }

        return test is TestSuite parent
            ? parent.Tests.Select(child => Find(child, fullName)).OfType<TestSuite>().FirstOrDefault()
            : null;
    }
}
