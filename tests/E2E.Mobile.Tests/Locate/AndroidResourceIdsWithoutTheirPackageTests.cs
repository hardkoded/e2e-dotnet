// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using static E2E.Mobile.Tests.RawNodes;

namespace E2E.Mobile.Tests.Locate;

public sealed class AndroidResourceIdsWithoutTheirPackageTests
{
    private const string App = "dev.e2e.examples.maui";

    private static RawNode[] AndroidIdNodes() =>
    [
        N("@e1", index: 0, depth: 0, type: "android.widget.FrameLayout", identifier: "android:id/content", rect: new Rect(0, 0, 390, 844)),
        N("@e2", index: 1, parentIndex: 0, depth: 1, type: "android.view.ViewGroup", identifier: $"{App}:id/form", rect: new Rect(0, 100, 390, 300)),
        N("@e3", index: 2, parentIndex: 1, depth: 2, type: "android.widget.EditText", identifier: $"{App}:id/name", label: "Ada Lovelace", hintShowing: true, rect: new Rect(0, 120, 390, 44)),
        N("@e4", index: 3, parentIndex: 1, depth: 2, type: "android.widget.Button", identifier: $"{App}:id/greet", label: "Greet", rect: new Rect(0, 180, 390, 44)),
        N("@e5", index: 4, parentIndex: 0, depth: 1, type: "android.widget.TextView", identifier: "com.example.other:id/banner", label: "Elsewhere", rect: new Rect(0, 420, 390, 44)),
        N("@e6", index: 5, parentIndex: 0, depth: 1, type: "android.widget.TextView", identifier: "greeting", label: "Hello, Ada!", rect: new Rect(0, 480, 390, 44)),
    ];

    private static LocatorExpression TestId(string value) => LocatorExpression.For("testid", TextMatch.FromString(value), new TextMatchOptions(), nameof(value));

    private static List<string> TestIds(LocatorExpression expression, MobilePlatform platform = MobilePlatform.Android, IEnumerable<RawNode>? extra = null)
    {
        var index = Project([.. AndroidIdNodes(), .. extra ?? []]).Index;
        return [.. DeviceLocator.ResolveExpression(expression, index, platform).Select(entry => entry.Node.TestId ?? "")];
    }

    [Fact]
    public void Finds_an_Android_view_by_its_id_without_the_package_prefix()
    {
        Assert.Equal([$"{App}:id/greet"], TestIds(TestId("greet")));
    }

    [Fact]
    public void Still_finds_an_Android_view_by_its_full_resource_id()
    {
        Assert.Equal([$"{App}:id/greet"], TestIds(TestId($"{App}:id/greet")));
    }

    [Fact]
    public void Answers_an_id_the_app_set_without_a_prefix_as_before()
    {
        Assert.Equal(["greeting"], TestIds(TestId("greeting")));
    }

    [Fact]
    public void Leaves_the_prefix_off_any_package_but_the_framework()
    {
        Assert.Equal(["com.example.other:id/banner"], TestIds(TestId("banner")));
        Assert.Empty(TestIds(TestId("content")));
        Assert.Equal(["android:id/content"], TestIds(TestId("android:id/content")));
    }

    [Fact]
    public void Prefers_a_full_match_so_a_bare_id_wins_over_a_prefixed_one_with_the_same_name()
    {
        var extra = new[] { N("@e7", index: 6, parentIndex: 0, depth: 1, type: "android.widget.Button", identifier: "greet", label: "Greet again") };
        Assert.Equal(["greet"], TestIds(TestId("greet"), extra: extra));
    }

    [Fact]
    public void Keeps_an_apps_own_id_clear_of_a_system_windows_with_the_same_name()
    {
        var extra = new[]
        {
            N("@e7", index: 6, parentIndex: 0, depth: 1, type: "android.widget.TextView", identifier: "clock", label: "12:00"),
            N("@e8", index: 7, parentIndex: 0, depth: 1, type: "android.widget.TextView", identifier: "com.android.systemui:id/clock", label: "9:41"),
        };
        Assert.Equal(["clock"], TestIds(TestId("clock"), extra: extra));
    }

    [Fact]
    public void Answers_every_prefixed_id_with_the_name_when_none_matches_in_full_for_the_runner_to_refuse_as_ambiguous()
    {
        var extra = new[]
        {
            N("@e7", index: 6, parentIndex: 0, depth: 1, type: "android.widget.TextView", identifier: $"{App}:id/clock", label: "12:00"),
            N("@e8", index: 7, parentIndex: 0, depth: 1, type: "android.widget.TextView", identifier: "com.android.systemui:id/clock", label: "9:41"),
        };
        Assert.Equal([$"{App}:id/clock", "com.android.systemui:id/clock"], TestIds(TestId("clock"), extra: extra));
    }

    [Fact]
    public void Tests_a_Regex_against_the_id_without_the_prefix_too()
    {
        static LocatorExpression Pattern(string source) => LocatorExpression.For("testid", TextMatch.FromRegex(new Regex(source)), new TextMatchOptions(), "value");

        Assert.Equal([$"{App}:id/greet"], TestIds(Pattern("^greet$")));
        Assert.Equal([$"{App}:id/greet"], TestIds(Pattern(@"^dev\.e2e\.examples\.maui:id/gr")));
    }

    [Fact]
    public void Applies_the_short_form_to_scopes_and_has_filters()
    {
        var form = TestId("form");
        var scoped = TestId("greet") with { Parent = form };
        Assert.Equal([$"{App}:id/greet"], TestIds(scoped));
        Assert.Equal([$"{App}:id/form"], TestIds(new LocatorExpression { Kind = "filter", Source = form, Has = TestId("name") }));
    }

    [Fact]
    public void Leaves_iOS_identifiers_as_they_are_whatever_they_spell()
    {
        Assert.Empty(TestIds(TestId("greet"), MobilePlatform.Ios));
    }
}
