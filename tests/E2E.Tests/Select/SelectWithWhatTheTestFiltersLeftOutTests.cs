// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.Select.SelectKit;

namespace E2E.Tests.Select;

/// <summary>
/// Upstream's <c>describe('select with what the test filters left out')</c> (<c>tests/unit/select.test.ts</c>). Every <c>it</c> is partial:
/// it builds its collection by hand where upstream collects <c>test()</c> declarations with <c>collectModule</c>, and its config by hand
/// where upstream calls <c>resolveConfig</c>.
/// </summary>
public sealed class SelectWithWhatTheTestFiltersLeftOutTests
{
    [Fact]
    public void Partial_Leaves_the_test_out_on_every_target_and_agent_naming_the_filter_and_its_reason()
    {
        var col = CollectionOf("tests/a.e2e.ts", new Decl("pays") { Agents = ["default", "admin"] }, new Decl("refunds") { Agents = ["default", "admin"] });
        var cfg = ConfigOf(targets: [Web("web"), Web("web-2")]);
        var selection = Selector.Select(col, cfg, new SelectionFilters { Excluded = LeftOut(col, ("refunds", "areas", "the diff misses it")) });
        var refunds = selection.Pairs.Where(pair => pair.Test.Title == "refunds").ToList();
        Assert.Equal([("web", "default"), ("web", "admin"), ("web-2", "default"), ("web-2", "admin")], refunds.Select(pair => (pair.Target.Name, pair.Agent)));
        foreach (var pair in refunds)
        {
            Assert.Equal(Disposition.Filtered, pair.Disposition);
            Assert.Equal(new SkipInfo("filtered", "the diff misses it", Filter: "areas"), pair.Skip);
        }

        Assert.All(selection.Pairs.Where(pair => pair.Disposition == Disposition.Run), pair =>
        {
            Assert.Equal("pays", pair.Test.Title);
            Assert.Null(pair.Skip);
        });
    }

    [Fact]
    public void Partial_Brings_a_left_out_serial_member_back_with_a_kept_one_and_drops_a_group_left_out_whole()
    {
        var col = CollectionOf(
            "tests/a.e2e.ts",
            new Decl("step 1") { Describe = ["wizard"], Serial = "wizard" },
            new Decl("step 2") { Describe = ["wizard"], Serial = "wizard" },
            new Decl("other"));
        var one = Selector.Select(col, ConfigOf(), new SelectionFilters { Excluded = LeftOut(col, ("step 1", "areas", "unaffected")) });
        Assert.Equal(["step 1", "step 2", "other"], Running(one));
        Assert.Null(one.Pairs.First(pair => pair.Test.Title == "step 1").Skip);

        var both = Selector.Select(col, ConfigOf(), new SelectionFilters { Excluded = LeftOut(col, ("step 1", "areas", "unaffected"), ("step 2", "areas", "unaffected")) });
        Assert.Equal(["other"], Running(both));
        Assert.Equal(
            [new SkipInfo("filtered", "unaffected", Filter: "areas"), new SkipInfo("filtered", "unaffected", Filter: "areas")],
            both.Pairs.Where(pair => pair.Test.SerialId is not null).Select(pair => pair.Skip));
    }

    [Fact]
    public void Partial_Runs_a_setup_only_for_the_consumers_the_filters_kept()
    {
        var col = CollectionOf(
            "tests/a.e2e.ts",
            new Decl("sign in") { Sessions = ["member"] },
            new Decl("signed in") { Session = "member" },
            new Decl("anonymous"));
        var cfg = ConfigOf(targets: [Web("web", StateEngine)]);
        var consumerOut = Selector.Select(col, cfg, new SelectionFilters { Excluded = LeftOut(col, ("signed in", "areas", "unaffected")) });
        Assert.Equal(["anonymous"], Running(consumerOut));
        var consumerKept = Selector.Select(col, cfg, new SelectionFilters { Excluded = LeftOut(col, ("anonymous", "areas", "unaffected")) });
        Assert.Equal(["sign in", "signed in"], Running(consumerKept));
    }

    [Fact]
    public void Partial_Cuts_shard_on_the_unfiltered_selection_then_leaves_out_only_this_shards_pairs()
    {
        var col = CollectionOf("tests/a.e2e.ts", new Decl("a"), new Decl("b"), new Decl("c"), new Decl("d"));
        var outside = new SkipInfo("filtered", "outside the shard");
        var unaffected = new SkipInfo("filtered", "unaffected", Filter: "areas");

        // Leaving b out does not move the cut: shard 1 is still a and b, shard 2 still c and d.
        var excluded = LeftOut(col, ("b", "areas", "unaffected"));
        var first = Selector.Select(col, ConfigOf(), new SelectionFilters { Excluded = excluded, Shard = new Shard(1, 2) });
        Assert.Equal(["a"], Running(first));
        Assert.Equal(unaffected, Pair(first, "b").Skip);
        Assert.Equal(outside, Pair(first, "c").Skip);
        Assert.Equal(outside, Pair(first, "d").Skip);
        var second = Selector.Select(col, ConfigOf(), new SelectionFilters { Excluded = excluded, Shard = new Shard(2, 2) });
        Assert.Equal(["c", "d"], Running(second));
        Assert.Equal(outside, Pair(second, "a").Skip);
        Assert.Equal(outside, Pair(second, "b").Skip);
    }

    [Fact]
    public void Partial_Leaves_out_only_what_would_run_platform_and_capability_verdicts_on_another_target_stand()
    {
        var col = CollectionOf(
            "tests/a.e2e.ts",
            new Decl("web only") { Platforms = ["web"] },
            new Decl("needs device") { Requires = ["device"] },
            new Decl("kept"));
        var cfg = ConfigOf(targets: [Web("web", new ResolvedEngine("toy", new HashSet<string>())), Web("ios", platform: "ios")]);
        var filtered = Selector.Select(col, cfg, new SelectionFilters { Excluded = LeftOut(col, ("web only", "areas", "unaffected"), ("needs device", "areas", "unaffected")) });
        Assert.Equal(new SkipInfo("filtered", "unaffected", Filter: "areas"), Pair(filtered, "web only", "web").Skip);
        Assert.Equal("platform-unavailable", Pair(filtered, "web only", "ios").Skip?.Cause);
        Assert.Equal("capability-unavailable", Pair(filtered, "needs device", "web").Skip?.Cause);
    }

    private static TestTargetPair Pair(Selection selection, string title, string? target = null) =>
        selection.Pairs.First(entry => entry.Test.Title == title && (target is null || entry.Target.Name == target));
}
