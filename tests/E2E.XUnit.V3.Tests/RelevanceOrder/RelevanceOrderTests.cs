// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.XUnit.V3.Tests;

namespace E2E.XUnit.V3.Tests.RelevanceOrder;

/// <summary>
/// .NET-only (upstream orders its work plans in the runner, which the port has not; xUnit owns the order of the tests): the scores the
/// filters gave are kept by test key, and a class ranks by the highest relevance band of its tests, as <c>orderWorkPlans</c> ranks a file.
/// </summary>
[Collection(nameof(RunSelection.RunSelectionCollection))]
public sealed class RelevanceOrderTests
{
    [Fact]
    public void Ranks_a_class_by_the_highest_relevance_band_of_its_tests()
    {
        E2E.XUnit.V3.RunSelection.SetRelevance(
            new Dictionary<string, double>
            {
                ["A.Tests.Alpha"] = 0.3,
                ["A.Tests.Beta"] = 0.9,
                ["B.Tests.Gamma"] = 0.1,
            }
        );
        try
        {
            Assert.Equal(3, E2E.XUnit.V3.RunSelection.BandOfClass("A.Tests"));
            Assert.Equal(0, E2E.XUnit.V3.RunSelection.BandOfClass("B.Tests"));
            Assert.Equal(0, E2E.XUnit.V3.RunSelection.BandOfClass("C.Tests"));
        }
        finally
        {
            E2E.XUnit.V3.RunSelection.SetRelevance(new Dictionary<string, double>());
        }
    }
}
