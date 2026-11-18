// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.Units.UnitsKit;
using UnitPlanning = E2E.Units;

namespace E2E.Tests.Units;

/// <summary>Upstream's <c>describe('buildWorkPlans')</c> (<c>tests/unit/units.test.ts</c>).</summary>
public sealed class BuildWorkPlansTests
{
    [Fact]
    public void Groups_runnable_ordinary_pairs_into_per_file_units_and_setups_into_single_pair_units()
    {
        var setup = MakeTest("tests/auth.setup.e2e.ts", "login", 0, TestKind.Setup, ["user"]);
        var a1 = MakeTest("tests/a.e2e.ts", "a one", 0);
        var a2 = MakeTest("tests/a.e2e.ts", "a two", 1);
        var b1 = MakeTest("tests/b.e2e.ts", "b one", 0);
        var skipped = MakeTest("tests/b.e2e.ts", "b skipped", 1);

        TestTargetPair[] pairs =
        [
            MakePair(setup),
            MakePair(a1),
            MakePair(a2),
            MakePair(b1),
            MakePair(skipped, disposition: Disposition.Skip, skip: new SkipInfo("explicit", "skipped")),
        ];
        var selection = new Selection(pairs, [new TargetSelection(Target, pairs)]);
        var collection = new Collection
        {
            Files = [MakeFile("tests/a.e2e.ts"), MakeFile("tests/auth.setup.e2e.ts"), MakeFile("tests/b.e2e.ts")],
            Tests = [a1, a2, setup, b1, skipped],
        };

        var plans = UnitPlanning.BuildWorkPlans(selection, collection, Path.GetFullPath("/project"));
        var plan = Assert.Single(plans);
        var setupUnit = Assert.Single(plan.SetupUnits);
        Assert.Equal("setup", setupUnit.Kind);
        Assert.Equal(["login"], setupUnit.Pairs.Select(pair => pair.Test.Title));
        Assert.Equal(["tests/a.e2e.ts", "tests/b.e2e.ts"], plan.FileUnits.Select(unit => unit.File));
        Assert.Equal(["a one", "a two"], plan.FileUnits[0].Pairs.Select(pair => pair.Test.Title));
        Assert.Equal(["b one"], plan.FileUnits[1].Pairs.Select(pair => pair.Test.Title));
        Assert.Equal(["b skipped"], plan.Immediate.Select(pair => pair.Test.Title));
    }

    [Fact]
    public void Emits_no_file_unit_when_a_file_has_no_runnable_pairs()
    {
        var only = MakeTest("tests/a.e2e.ts", "filtered", 0);
        TestTargetPair[] pairs =
        [
            MakePair(only, disposition: Disposition.Filtered, skip: new SkipInfo("filtered", "tags")),
        ];
        var selection = new Selection(pairs, [new TargetSelection(Target, pairs)]);
        var collection = new Collection { Files = [MakeFile("tests/a.e2e.ts")], Tests = [only] };
        var plans = UnitPlanning.BuildWorkPlans(selection, collection, Path.GetFullPath("/project"));
        Assert.Empty(plans[0].FileUnits);
        Assert.Single(plans[0].Immediate);
    }
}
