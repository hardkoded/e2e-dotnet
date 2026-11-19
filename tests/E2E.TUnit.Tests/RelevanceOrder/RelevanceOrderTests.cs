// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using TUnit.Core.Enums;
using TUnitContext = global::TUnit.Core.TestContext;

namespace E2E.TUnit.Tests.RelevanceOrder;

/// <summary>
/// .NET-only (upstream orders its work plans in the runner, which the port has not; TUnit owns the order of the tests): a test starts in the
/// priority of the relevance band its score falls in, as <c>orderWorkPlans</c> ranks a file, and never in a lower one than it has.
/// </summary>
[NotInParallel(nameof(RelevanceOrderTests))]
public sealed class RelevanceOrderTests
{
    [Test]
    [Arguments(0, Priority.Normal)]
    [Arguments(1, Priority.AboveNormal)]
    [Arguments(2, Priority.High)]
    [Arguments(3, Priority.Critical)]
    public async Task Gives_each_relevance_band_a_priority_above_the_one_below(int band, Priority expected) =>
        await Assert.That(E2E.TUnit.RunSelection.PriorityOf(band)).IsEqualTo(expected);

    [Test]
    public async Task Ranks_a_registered_test_by_its_score_and_keeps_a_higher_priority()
    {
        var context = TUnitContext.Current!;
        var details = context.Metadata.TestDetails;
        var key = $"{details.ClassType.FullName}.{details.MethodName}";
        var before = context.Parallelism.ExecutionPriority;
        try
        {
            context.Parallelism.ExecutionPriority = Priority.Low;
            E2E.TUnit.RunSelection.SetRelevance(new Dictionary<string, double> { [key] = 0.6 });
            E2E.TUnit.RunSelection.RankRegistered(context);
            await Assert.That(context.Parallelism.ExecutionPriority).IsEqualTo(Priority.High);

            context.Parallelism.ExecutionPriority = Priority.Critical;
            E2E.TUnit.RunSelection.RankRegistered(context);
            await Assert.That(context.Parallelism.ExecutionPriority).IsEqualTo(Priority.Critical);
        }
        finally
        {
            E2E.TUnit.RunSelection.SetRelevance(new Dictionary<string, double>());
            context.Parallelism.ExecutionPriority = before;
        }
    }
}
