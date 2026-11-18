// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.Units.UnitsKit;
using UnitPlanning = E2E.Units;

namespace E2E.Tests.Units;

/// <summary>Upstream's <c>describe('orderWorkPlans')</c> (<c>tests/unit/units.test.ts</c>).</summary>
public sealed class OrderWorkPlansTests
{
    private static readonly string[] Files =
    [
        "tests/a.e2e.ts",
        "tests/b.e2e.ts",
        "tests/c.e2e.ts",
        "tests/d.e2e.ts",
        "tests/e.e2e.ts",
    ];

    /// <summary>The id of test <paramref name="index"/> of <paramref name="file"/>, as <c>PlansFor</c> makes them.</summary>
    private static string IdOf(string file, int index = 0) => $"test::{file}::{file}#{index}";

    /// <summary>Work plans for the targets named by the keys of <paramref name="files"/>, in that order, each running the files it maps to, <paramref name="testsPerFile"/> tests of each.</summary>
    private static List<TargetWorkPlan> PlansFor(
        IReadOnlyList<(string Name, string[] Files)> files,
        int testsPerFile = 1
    )
    {
        var all = files.SelectMany(entry => entry.Files).Distinct().ToList();
        var tests = all.ToDictionary(
            file => file,
            file => Enumerable.Range(0, testsPerFile).Select(index => MakeTest(file, $"{file}#{index}", index)).ToList()
        );
        var perTarget = files
            .Select(
                (entry, index) =>
                {
                    var named = new ResolvedTarget(entry.Name, "web", new AppConfig());
                    return new TargetSelection(
                        named,
                        [.. entry.Files.SelectMany(file => tests[file].Select(test => MakePair(test, named)))]
                    );
                }
            )
            .ToList();
        var collection = new Collection
        {
            Files = [.. all.Select(MakeFile)],
            Tests = [.. tests.Values.SelectMany(list => list)],
        };
        return UnitPlanning.BuildWorkPlans(
            new Selection([.. perTarget.SelectMany(entry => entry.Pairs)], perTarget),
            collection,
            Path.GetFullPath("/project")
        );
    }

    private static List<TargetWorkPlan> PlansFor(string name, params string[] files) => PlansFor([(name, files)]);

    /// <summary>The files of each plan in dispatch order, keyed by target.</summary>
    private static List<(string Target, string[] Files)> Dispatched(IEnumerable<TargetWorkPlan> plans) =>
        [.. plans.Select(plan => (plan.Target.Name, plan.FileUnits.Select(unit => unit.File).ToArray()))];

    private static void AssertDispatched(
        IEnumerable<(string Target, string[] Files)> expected,
        IEnumerable<TargetWorkPlan> plans
    )
    {
        Assert.Equal(
            expected.Select(entry => $"{entry.Target}: {string.Join(", ", entry.Files)}"),
            Dispatched(plans).Select(entry => $"{entry.Target}: {string.Join(", ", entry.Files)}")
        );
    }

    private static double None(string _) => 0;

    private static double? Untimed(string target, string file) => null;

    [Fact]
    public void Dispatches_the_files_the_previous_run_spent_longest_in_first_untimed_ones_before_them_ties_in_collection_order()
    {
        var previous = new Dictionary<string, double>
        {
            ["tests/a.e2e.ts"] = 100,
            ["tests/b.e2e.ts"] = 900,
            ["tests/d.e2e.ts"] = 100,
        };
        var plans = UnitPlanning.OrderWorkPlans(
            PlansFor("web", Files),
            new WorkOrder(None, (target, file) => target == "web" && previous.TryGetValue(file, out var ms) ? ms : null)
        );
        AssertDispatched(
            [("web", ["tests/c.e2e.ts", "tests/e.e2e.ts", "tests/b.e2e.ts", "tests/a.e2e.ts", "tests/d.e2e.ts"])],
            plans
        );
    }

    [Fact]
    public void Keeps_collection_order_when_no_file_has_a_score_or_a_timing()
    {
        var plans = PlansFor("web", Files);
        AssertDispatched([("web", Files)], UnitPlanning.OrderWorkPlans(plans, new WorkOrder(None, Untimed)));
    }

    [Fact]
    public void Starts_the_likeliest_band_first_whatever_the_timings_say_the_longest_file_first_inside_a_band_and_an_untimed_file_before_the_timed_ones()
    {
        string[] files = [.. Files, "tests/f.e2e.ts"];
        var scores = new Dictionary<string, double>
        {
            [IdOf("tests/a.e2e.ts")] = 0.9,
            [IdOf("tests/b.e2e.ts")] = 0.8,

            // Each edge opens its own band: 0.5 is in the one above 0.49, and 0.24 is under the last.
            [IdOf("tests/c.e2e.ts")] = 0.5,
            [IdOf("tests/d.e2e.ts")] = 0.49,
            [IdOf("tests/e.e2e.ts")] = 0.24,
        };
        var durations = new Dictionary<string, double>
        {
            ["tests/a.e2e.ts"] = 100,
            ["tests/b.e2e.ts"] = 900,
            ["tests/c.e2e.ts"] = 10,
            ["tests/d.e2e.ts"] = 5000,
            ["tests/e.e2e.ts"] = 700,
        };
        var plans = UnitPlanning.OrderWorkPlans(
            PlansFor("web", files),
            new WorkOrder(
                id => scores.GetValueOrDefault(id),
                (_, file) => durations.TryGetValue(file, out var ms) ? ms : null
            )
        );

        // d ran longest and is not first: its band is under c's. f has no timing, so it is ahead of e in the last band.
        AssertDispatched(
            [
                (
                    "web",
                    [
                        "tests/b.e2e.ts",
                        "tests/a.e2e.ts",
                        "tests/c.e2e.ts",
                        "tests/d.e2e.ts",
                        "tests/f.e2e.ts",
                        "tests/e.e2e.ts",
                    ]
                ),
            ],
            plans
        );
    }

    [Fact]
    public void Ranks_a_file_by_the_likeliest_of_its_tests_and_a_test_no_one_scored_as_0()
    {
        string[] files = ["tests/a.e2e.ts", "tests/b.e2e.ts", "tests/c.e2e.ts"];
        var scores = new Dictionary<string, double>
        {
            [IdOf("tests/a.e2e.ts", 0)] = 0.3,
            [IdOf("tests/a.e2e.ts", 1)] = 0.4,
            [IdOf("tests/b.e2e.ts", 1)] = 0.8,
        };
        var plans = UnitPlanning.OrderWorkPlans(
            PlansFor([("web", files)], 2),
            new WorkOrder(id => scores.GetValueOrDefault(id), Untimed)
        );
        AssertDispatched([("web", ["tests/b.e2e.ts", "tests/a.e2e.ts", "tests/c.e2e.ts"])], plans);
    }

    [Fact]
    public void Ranks_a_file_by_its_tests_on_every_repeat()
    {
        var plan = PlansFor("web", "tests/a.e2e.ts", "tests/b.e2e.ts")[0];
        var second = plan.FileUnits[1];
        var repeated = plan with
        {
            FileUnits =
            [
                plan.FileUnits[0],
                second with
                {
                    Pairs = [.. Enumerable.Range(0, 3).Select(repeat => second.Pairs[0] with { Repeat = repeat })],
                },
            ],
        };
        var ordered = UnitPlanning.OrderWorkPlans(
            [repeated],
            new WorkOrder(id => id == IdOf("tests/b.e2e.ts") ? 0.6 : 0, Untimed)
        );
        AssertDispatched([("web", ["tests/b.e2e.ts", "tests/a.e2e.ts"])], ordered);
    }

    [Fact]
    public void Starts_the_target_with_the_likeliest_file_first_ties_keeping_config_order_and_orders_the_files_inside_each()
    {
        var scores = new Dictionary<string, double>
        {
            [IdOf("tests/c.e2e.ts")] = 0.3,
            [IdOf("tests/d.e2e.ts")] = 0.9,
            [IdOf("tests/g.e2e.ts")] = 0.3,
        };
        var plans = UnitPlanning.OrderWorkPlans(
            PlansFor([
                ("web", ["tests/a.e2e.ts", "tests/b.e2e.ts"]),
                ("ios", ["tests/c.e2e.ts", "tests/e.e2e.ts"]),
                ("android", ["tests/f.e2e.ts", "tests/d.e2e.ts"]),
                ("tablet", ["tests/g.e2e.ts", "tests/h.e2e.ts"]),
            ]),
            new WorkOrder(id => scores.GetValueOrDefault(id), Untimed)
        );
        AssertDispatched(
            [
                ("android", ["tests/d.e2e.ts", "tests/f.e2e.ts"]),
                ("ios", ["tests/c.e2e.ts", "tests/e.e2e.ts"]),
                ("tablet", ["tests/g.e2e.ts", "tests/h.e2e.ts"]),
                ("web", ["tests/a.e2e.ts", "tests/b.e2e.ts"]),
            ],
            plans
        );
    }

    [Fact]
    public void Leaves_the_plans_in_config_order_when_nothing_is_scored_a_target_with_no_file_to_run_keeping_its_place()
    {
        var plans = PlansFor([("web", ["tests/a.e2e.ts"]), ("ios", []), ("android", ["tests/b.e2e.ts"])]);
        Assert.Equal(
            ["web", "ios", "android"],
            Dispatched(UnitPlanning.OrderWorkPlans(plans, new WorkOrder(None, Untimed))).Select(entry => entry.Target)
        );
    }

    [Fact]
    public void Keeps_each_plans_setup_units_immediate_pairs_and_everything_else_it_carries()
    {
        var plan = PlansFor("web", "tests/a.e2e.ts", "tests/b.e2e.ts")[0];
        var withExtras = plan with
        {
            Workers = 2,
            Env = new Dictionary<string, string> { ["A"] = "1" },
        };
        var ordered = Assert.Single(
            UnitPlanning.OrderWorkPlans(
                [withExtras],
                new WorkOrder(id => id == IdOf("tests/b.e2e.ts") ? 1 : 0, Untimed)
            )
        );
        Assert.Equal(withExtras.Target, ordered.Target);
        Assert.Equal(withExtras.SetupUnits, ordered.SetupUnits);
        Assert.Equal(withExtras.Immediate, ordered.Immediate);
        Assert.Equal(2, ordered.Workers);
        Assert.Equal(withExtras.Env, ordered.Env);
        Assert.Equal(["tests/b.e2e.ts", "tests/a.e2e.ts"], ordered.FileUnits.Select(unit => unit.File));
    }
}
