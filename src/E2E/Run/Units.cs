// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// One schedulable unit (upstream's <c>WorkUnit</c>, <c>run/units.ts</c>): either a single setup pair or every runnable ordinary pair of one
/// file for one target. Units execute on exactly one worker.
/// </summary>
/// <param name="Id">The unit's id: <c>setup::&lt;target&gt;::&lt;test id&gt;</c> or <c>file::&lt;target&gt;::&lt;file&gt;</c>.</param>
/// <param name="Kind"><c>setup</c> or <c>file</c>.</param>
/// <param name="TargetName">The target the unit runs on.</param>
/// <param name="File">Normalized project-root-relative file path.</param>
/// <param name="AbsolutePath">The file's absolute path.</param>
/// <param name="Pairs">Pairs with disposition <c>run</c>, in declaration order.</param>
internal sealed record WorkUnit(
    string Id,
    string Kind,
    string TargetName,
    string File,
    string AbsolutePath,
    IReadOnlyList<TestTargetPair> Pairs
);

/// <summary>Per-target schedule (upstream's <c>TargetWorkPlan</c>): setup units gate file units.</summary>
internal sealed record TargetWorkPlan
{
    public required ResolvedTarget Target { get; init; }

    public required IReadOnlyList<WorkUnit> SetupUnits { get; init; }

    public required IReadOnlyList<WorkUnit> FileUnits { get; init; }

    /// <summary>Non-run ordinary pairs the scheduler reports without dispatching.</summary>
    public required IReadOnlyList<TestTargetPair> Immediate { get; init; }

    /// <summary>Worker cap the engine reported from <c>prepare</c>, over its declared <c>workers</c>.</summary>
    public int? Workers { get; init; }

    /// <summary>Variables the engine's <c>prepare</c> handed this target's workers.</summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }
}

/// <summary>What <see cref="Units.OrderWorkPlans"/> ranks the work by (upstream's <c>WorkOrder</c>).</summary>
/// <param name="RelevanceOf">How likely the test filters rated a test to fail on this change, from 0 to 1; 0 for a test none scored.</param>
/// <param name="DurationOf">How long the previous run spent in a target's file; null when it has no timing, as every file does when the run reads none.</param>
internal sealed record WorkOrder(Func<string, double> RelevanceOf, Func<string, string, double?> DurationOf);

/// <summary>Work-unit planning (upstream's <c>run/units.ts</c>).</summary>
internal static class Units
{
    /// <summary>The scores at which a test enters the next higher relevance band, so a score of at least 0.75 is band 3, and under 0.25 band 0.</summary>
    private static readonly double[] RelevanceEdges = [0.25, 0.5, 0.75];

    /// <summary>Builds per-target work plans from a selection, preserving report order (upstream's <c>buildWorkPlans</c>).</summary>
    public static List<TargetWorkPlan> BuildWorkPlans(Selection selection, Collection collection, string projectRoot) =>
        [
            .. selection.PerTarget.Select(entry =>
            {
                var (target, pairs) = (entry.Target, entry.Pairs);

                // One pass sorts every pair into its bucket; files are then visited in collection order so unit order (and thus report order) is stable.
                var setupUnits = new List<WorkUnit>();
                var runnableByFile = new Dictionary<string, List<TestTargetPair>>(StringComparer.Ordinal);
                var immediate = new List<TestTargetPair>();
                foreach (var pair in pairs)
                {
                    if (pair.Test.Kind == TestKind.Setup)
                    {
                        if (pair.Disposition != Disposition.Run)
                        {
                            continue;
                        }

                        setupUnits.Add(
                            new WorkUnit(
                                $"setup::{target.Name}::{pair.Test.Id}",
                                "setup",
                                target.Name,
                                pair.Test.File,
                                Path.GetFullPath(Path.Combine(projectRoot, pair.Test.File)),
                                [pair]
                            )
                        );
                    }
                    else if (pair.Disposition != Disposition.Run)
                    {
                        immediate.Add(pair);
                    }
                    else if (runnableByFile.TryGetValue(pair.Test.File, out var bucket))
                    {
                        bucket.Add(pair);
                    }
                    else
                    {
                        runnableByFile[pair.Test.File] = [pair];
                    }
                }

                var fileUnits = new List<WorkUnit>();
                foreach (var file in collection.Files)
                {
                    if (!runnableByFile.TryGetValue(file.File, out var filePairs))
                    {
                        continue;
                    }

                    fileUnits.Add(
                        new WorkUnit(
                            $"file::{target.Name}::{file.File}",
                            "file",
                            target.Name,
                            file.File,
                            Path.GetFullPath(Path.Combine(projectRoot, file.File)),
                            [.. filePairs.OrderBy(pair => pair.Test.DeclarationIndex)]
                        )
                    );
                }

                return new TargetWorkPlan
                {
                    Target = target,
                    SetupUnits = setupUnits,
                    FileUnits = fileUnits,
                    Immediate = immediate,
                };
            }),
        ];

    /// <summary>The skip of a test whose session-producing setup test did not pass (upstream's <c>setupFailedSkip</c>).</summary>
    public static SkipInfo SetupFailedSkip(string session, string setupTestId) =>
        new("setup-failed", $"setup for session \"{session}\" failed", setupTestId);

    /// <summary>The relevance band of a score: how many of the edges it reaches (upstream's <c>relevanceBand</c>).</summary>
    public static int RelevanceBand(double score) => RelevanceEdges.Count(edge => score >= edge);

    /// <summary>
    /// Orders each plan's file units, and the plans, so the work likeliest to fail starts first and long files start early. A file ranks by
    /// the highest relevance band of the tests it runs, and within a band longest first by the time the previous run spent in it, so a long
    /// file starts while short ones fill the other workers instead of starting last and running alone. Files without a timing go first, being
    /// of unknown length; ties keep collection order. A target ranks by its best file, ties keeping config order. Report order does not
    /// depend on dispatch order (upstream's <c>orderWorkPlans</c>).
    /// </summary>
    public static List<TargetWorkPlan> OrderWorkPlans(IReadOnlyList<TargetWorkPlan> plans, WorkOrder order)
    {
        var ranked = plans
            .Select(plan =>
            {
                // A stable sort, as `toSorted` is: LINQ's OrderBy keeps the order of equal keys.
                var units = plan
                    .FileUnits.Select(unit =>
                        (
                            Unit: unit,
                            Band: unit.Pairs.Aggregate(0, (best, pair) =>
                                Math.Max(best, RelevanceBand(order.RelevanceOf(pair.Test.Id)))
                            ),
                            Duration: order.DurationOf(unit.TargetName, unit.File) ?? double.PositiveInfinity
                        )
                    )
                    .OrderByDescending(entry => entry.Band)
                    .ThenByDescending(entry => entry.Duration)
                    .ToList();
                return (
                    Plan: plan with
                    {
                        FileUnits = [.. units.Select(entry => entry.Unit)],
                    },
                    Band: units.Count == 0 ? 0 : units[0].Band
                );
            })
            .OrderByDescending(entry => entry.Band);
        return [.. ranked.Select(entry => entry.Plan)];
    }
}
