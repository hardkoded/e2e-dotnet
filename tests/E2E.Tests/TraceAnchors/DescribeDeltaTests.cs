// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Internal;
using static E2E.Tests.TraceAnchors.AnchorFixtures;

namespace E2E.Tests.TraceAnchors;

/// <summary><c>describeDelta</c>: the step's delta, both what appeared and what vanished.</summary>
public sealed class DescribeDeltaTests
{
    [Fact]
    public void Keeps_what_appeared_and_what_vanished_compared_by_descriptor_rather_than_by_id()
    {
        // Same heading, freshly minted ref: not a delta.
        var (appeared, gone) = Describe(
            [Heading, EmptyMarker],
            [Node("heading", "Playbooks"), SavedMarker, Row, Toast]
        );

        Assert.Equal(["status|Marker~saved", "link|PB-Twin-Alpha", "|~Playbook saved"], Shapes(appeared));
        Assert.Equal(["status|Marker~empty"], Shapes(gone));
    }

    [Fact]
    public void Records_a_removal_as_what_vanished_so_a_delete_has_something_to_check()
    {
        var itemA = Node("listitem", "Item A");
        var itemB = Node("listitem", "Item B");

        var (appeared, gone) = Describe([Heading, itemA, itemB], [Heading, itemB]);

        Assert.Empty(appeared);
        Assert.Equal(["listitem|Item A"], Shapes(gone));
    }

    [Fact]
    public void Records_the_states_a_step_set_on_a_control_it_left_on_screen()
    {
        var off = Node("switch", "Email notifications", states: new NodeStates { Checked = false, Focused = false });
        var on = Node("switch", "Email notifications", states: new NodeStates { Checked = true, Focused = true });

        // Focus is where the pointer went, not what the step did.
        var (appeared, gone) = Describe([Heading, off], [Heading, on]);

        Assert.Equal(["switch|Email notifications|checked"], Shapes(appeared));
        Assert.Equal(["switch|Email notifications"], Shapes(gone));
    }

    [Fact]
    public void Strips_the_structural_selector_and_drops_nodes_nothing_could_relocate()
    {
        // The icon button carries a structural selector on its node; an anchor leaves it out, so it is a button with nothing else.
        var (appeared, _) = Describe(
            [],
            [Row, Node("button", selector: "[data-testid=\"icon\"] > svg"), Node("generic")]
        );

        Assert.Equal(["link|PB-Twin-Alpha"], Shapes(appeared));
        Assert.DoesNotContain(
            "selector",
            JsonSerializer.Serialize(appeared, JsonDefaults.Options),
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Deduplicates_repeated_descriptors_and_caps_each_side_at_the_anchor_limit_in_document_order()
    {
        var list = Enumerable
            .Range(0, Anchors.Max + 4)
            .Select(index => Node("listitem", text: "Row " + index))
            .ToList();
        var end = list.Append(Node("listitem", text: "Row 0")).ToList();

        var (appeared, _) = Describe([], end);

        Assert.Equal(Anchors.Max, appeared.Count);
        Assert.Equal("listitem|~Row 0", Shape(appeared[0]));
        Assert.Equal("listitem|~Row " + (Anchors.Max - 1), Shape(appeared[^1]));
        Assert.Equal(Shapes(appeared), Shapes(Describe(end, []).Gone));
    }

    [Fact]
    public void Records_the_value_a_step_typed_into_a_field_redacted_and_never_a_secure_fields()
    {
        static string Redact(string value) =>
            value.Replace("tok_9f8e7d6c5b4a3210", "<secret:token>", StringComparison.Ordinal);
        var empty = Node("textbox", "Email");
        var typed = Node("textbox", "Email", value: "ada@example.test");
        var leaked = Node("textbox", "Key", value: "tok_9f8e7d6c5b4a3210");

        var (appeared, gone) = Anchors.Describe(Project([empty], Redact), Project([typed, leaked], Redact), false);

        Assert.Equal(["textbox|Email=ada@example.test", "textbox|Key=<secret:token>"], Shapes(appeared));
        Assert.Equal(["textbox|Email"], Shapes(gone));
    }

    [Fact]
    public void Never_carries_a_secure_value_or_secret_plaintext()
    {
        static string Redact(string value) => value.Replace("hunter2", "<secret:password>", StringComparison.Ordinal);
        var password = Node("textbox", "Password", value: "hunter2", states: new NodeStates { Secure = true });
        var echo = Node(text: "you typed hunter2");

        var (appeared, _) = Anchors.Describe(Project([], Redact), Project([password, echo], Redact), false);

        Assert.DoesNotContain("hunter2", JsonSerializer.Serialize(appeared), StringComparison.Ordinal);
        Assert.Equal(["textbox|Password", "|~you typed <secret:password>"], Shapes(appeared));
    }

    [Fact]
    public void Puts_announcements_first_then_leaves_then_containers_which_only_repeat_their_children()
    {
        // Ten list items, each a container over one text leaf, plus a status line after the list:
        // 21 new nodes for a cap of 8.
        var roots = Enumerable
            .Range(0, 10)
            .Select(index =>
                Node("listitem", "Todo " + index + "Delete Todo " + index, children: [Node(text: "Todo " + index)])
            )
            .Append(Node("status", text: "Report ready"))
            .ToList();

        var (appeared, _) = Describe([], roots);

        Assert.Equal(Anchors.Max, appeared.Count);
        // The app's own announcement of the outcome is never what the cap cuts.
        Assert.Equal("status|~Report ready", Shape(appeared[0]));
        Assert.Equal(Enumerable.Range(0, Anchors.Max - 1).Select(index => "|~Todo " + index), Shapes(appeared.Skip(1)));
    }

    [Fact]
    public void Does_not_mistake_a_re_minted_test_id_for_a_new_node()
    {
        var before = Node("button", "Start sync", testId: "toggle-r1-2");
        var after = Node("button", "Start sync", testId: "toggle-r2-2");
        var effect = Node(text: "Activate plan is on");

        Assert.Equal(["|~Activate plan is on"], Shapes(Describe([before], [after, effect]).Appeared));

        // A node only its test id identifies still counts by that id.
        var idOnly = Node("generic", testId: "spinner");
        Assert.Equal(["generic|#spinner"], Shapes(Describe([], [idOnly]).Appeared));
    }

    [Fact]
    public void Skips_text_that_cannot_read_the_same_twice_while_a_stable_anchor_remains()
    {
        SemanticNode[] end =
        [
            Heading,
            Node(text: "sk_b1bccf4e03c5_..."),
            Node(text: "6 days 23 hours remaining"),
            Node(text: "Added Sep 8, 2026"),
            Node(text: "2026-09-08"),
            Node(text: "17:42"),
            Node(text: "Done in 321ms"),
            Node(text: "11"),
            Node(text: "Release pipeline"),
        ];

        Assert.Equal(["|~Release pipeline"], Shapes(Describe([Heading], end).Appeared));
    }

    [Fact]
    public void Keeps_volatile_anchors_when_nothing_stable_appeared_so_the_replay_hands_off_rather_than_passing_blind()
    {
        Assert.Equal(
            ["|~sk_b1bccf4e03c5_..."],
            Shapes(Describe([Heading], [Heading, Node(text: "sk_b1bccf4e03c5_...")]).Appeared)
        );
    }

    [Fact]
    public void Reads_an_alert_whose_countdown_ticked_during_the_step_as_the_alert_that_stayed()
    {
        static SemanticNode Banner(int minutes) => Node("alert", text: "Session expires in " + minutes + " minutes");

        var (appeared, gone) = Describe([Heading, Banner(5)], [Heading, Banner(4), Toast]);

        Assert.Equal(["|~Playbook saved"], Shapes(appeared));
        Assert.Empty(gone);
    }

    [Fact]
    public void Keeps_every_alert_whatever_its_text_since_a_replay_is_checked_for_alerts_it_raised()
    {
        var (appeared, _) = Describe(
            [Heading],
            [Heading, Node("alert", text: "Session expires at 17:42"), Node(text: "Release pipeline")]
        );

        Assert.Equal(["alert|~Session expires at 17:42", "|~Release pipeline"], Shapes(appeared));
    }

    [Fact]
    public void Does_not_mistake_progress_versions_short_ids_or_a_named_counter_for_volatile_text()
    {
        var (appeared, _) = Describe(
            [Heading],
            [
                Heading,
                Node(text: "3 / 30 steps"),
                Node(text: "v2.2.1"),
                Node(text: "E2E workspace 00d8365e"),
                Node("status", "Counter", text: "1"),
                Node(text: "Marketing 12"),
                Node(text: "Mayor 3"),
                Node(text: "Augusta 3"),
                Node(text: "12 Marketing"),
            ]
        );

        Assert.Equal(8, appeared.Count);
    }

    [Fact]
    public void Keeps_a_count_the_step_made_appear_which_is_its_result_and_skips_one_that_only_moved_or_that_came_with_another_route_which_is_the_data()
    {
        SemanticNode[] imported =
        [
            Heading,
            Node("status", text: "3 records imported"),
            Node(text: "Showing 1 to 9 of 9 results"),
        ];
        Assert.Equal(
            ["status|~3 records imported", "|~Showing 1 to 9 of 9 results"],
            Shapes(Describe([Heading], imported).Appeared)
        );

        // The same screen opened by a link: its counts are what the list holds today.
        Assert.Equal(
            ["|~Imported records"],
            Shapes(Describe([Heading], [.. imported, Node(text: "Imported records")], routeMoved: true).Appeared)
        );

        // A tally that was there before the step and reads another number after it grows with every run's leftovers.
        var (appeared, gone) = Describe(
            [Heading, Node(text: "41 items")],
            [Heading, Node(text: "42 items"), Node(text: "Item added")]
        );
        Assert.Equal(["|~Item added"], Shapes(appeared));
        Assert.Equal(["|~41 items"], Shapes(gone));
    }

    [Theory]
    [InlineData("41件", "42件")]
    [InlineData("41 件", "42 件")]
    [InlineData("41個", "42個")]
    [InlineData("41項目", "42項目")]
    [InlineData("41筆", "42筆")]
    [InlineData("共 41 条", "共 42 条")]
    [InlineData("41个", "42个")]
    [InlineData("41项", "42项")]
    [InlineData("41笔", "42笔")]
    [InlineData("총 41건", "총 42건")]
    [InlineData("41 개", "42 개")]
    [InlineData("41개", "42개")]
    [InlineData("共 41 条记录", "共 42 条记录")]
    [InlineData("41개의 결과", "42개의 결과")]
    [InlineData("41行", "42行")]
    [InlineData("41행", "42행")]
    [InlineData("41 개의 상품", "42 개의 상품")]
    [InlineData("41件 表示中", "42件 表示中")]
    [InlineData("全41件中", "全42件中")]
    [InlineData("41件の結果", "42件の結果")]
    [InlineData("共 41 条结果", "共 42 条结果")]
    [InlineData("41件記録しました", "42件記録しました")]
    [InlineData("共 41 筆結果", "共 42 筆結果")]
    [InlineData("41筆記錄", "42筆記錄")]
    [InlineData("41件を表示", "42件を表示")]
    [InlineData("41件が見つかりました", "42件が見つかりました")]
    [InlineData("41件は削除済み", "42件は削除済み")]
    [InlineData("41件も", "42件も")]
    [InlineData("41건이 검색되었습니다", "42건이 검색되었습니다")]
    [InlineData("41개가 있습니다", "42개가 있습니다")]
    [InlineData("41건을 처리했습니다", "42건을 처리했습니다")]
    [InlineData("41개를 선택", "42개를 선택")]
    [InlineData("41건은 처리되었습니다", "42건은 처리되었습니다")]
    [InlineData("41개는 품절", "42개는 품절")]
    [InlineData("41건도 확인", "42건도 확인")]
    [InlineData("41件です", "42件です")]
    [InlineData("41件でした", "42件でした")]
    [InlineData("全41件です。", "全42件です。")]
    [InlineData("총 41건입니다", "총 42건입니다")]
    [InlineData("41건이에요", "42건이에요")]
    [InlineData("41건이었습니다", "42건이었습니다")]
    [InlineData("41개예요", "42개예요")]
    [InlineData("41개였습니다", "42개였습니다")]
    public void Reads_a_count_with_a_CJK_counter_the_way_it_reads_an_English_one(string from, string to)
    {
        var (appeared, gone) = Describe(
            [Heading, Node(text: from)],
            [Heading, Node(text: to), Node(text: "Item added")]
        );

        Assert.Equal(["|~Item added"], Shapes(appeared));
        Assert.Equal(["|~" + from], Shapes(gone));
    }

    [Fact]
    public void Skips_a_CJK_count_on_a_screen_the_step_navigated_to()
    {
        Assert.Equal(
            ["|~インポートしたレコード"],
            Shapes(
                Describe(
                    [Heading],
                    [Heading, Node(text: "全9件中 1〜9件を表示"), Node(text: "インポートしたレコード")],
                    routeMoved: true
                ).Appeared
            )
        );
    }

    [Theory]
    [InlineData("ThinkPad X1笔记本电脑")]
    [InlineData("ThinkPad X1 筆記型電腦")]
    [InlineData("iPhone 15个人版")]
    [InlineData("3個人版")]
    [InlineData("3개월 무료 체험이 시작되었습니다")]
    [InlineData("3个月会员已开通")]
    [InlineData("3個月會員已開通")]
    [InlineData("第3条 個人情報の取扱い")]
    [InlineData("2024 개발자 컨퍼런스")]
    [InlineData("2025 건강검진 결과")]
    [InlineData("2024 행사 일정")]
    [InlineData("iPhone 15 개봉기")]
    [InlineData("2 行动计划")]
    [InlineData("2024 行程安排")]
    [InlineData("3行目を編集")]
    [InlineData("2024 行业报告")]
    [InlineData("2024 行業報告")]
    [InlineData("3 行動計画")]
    [InlineData("1 个性化设置")]
    [InlineData("3個性派")]
    [InlineData("2 条件满足")]
    [InlineData("3件目の注文")]
    [InlineData("3個目のボタン")]
    [InlineData("3 笔记本")]
    [InlineData("X13个")]
    [InlineData("2024 项目计划")]
    [InlineData("2024 筆電")]
    [InlineData("1 行星")]
    [InlineData("3条款")]
    [InlineData("15 個体")]
    [InlineData("2025건강검진")]
    [InlineData("2024개발자")]
    [InlineData("3개국어")]
    [InlineData("3行で書く")]
    [InlineData("2024 개도국 지원")]
    [InlineData("3行でも読める")]
    [InlineData("3行ですべて書く")]
    [InlineData("3個ですぐ届く")]
    [InlineData("2行でしたためる")]
    [InlineData("2024 개입 사례")]
    [InlineData("2024 건이동 안내")]
    public void Keeps_text_on_a_screen_the_step_navigated_to_since_the_digits_are_not_a_count(string text)
    {
        Assert.Equal(
            ["|~" + text, "|~インポートしたレコード"],
            Shapes(
                Describe(
                    [Heading],
                    [Heading, Node(text: text), Node(text: "インポートしたレコード")],
                    routeMoved: true
                ).Appeared
            )
        );
    }

    [Fact]
    public void Is_empty_when_nothing_changed()
    {
        var (appeared, gone) = Describe([Heading, EmptyMarker], [Heading, EmptyMarker]);

        Assert.Empty(appeared);
        Assert.Empty(gone);
    }
}
