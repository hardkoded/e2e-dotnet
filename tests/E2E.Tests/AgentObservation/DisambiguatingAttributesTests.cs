// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentObservation;

public sealed class DisambiguatingAttributesTests
{
    private static SemanticNode Link(string name, string href) =>
        new() { Ref = "n", Role = "link", Name = name, Attributes = new Dictionary<string, string> { ["href"] = href } };

    private static string[] Lines(IEnumerable<SemanticNode> nodes, string? appOrigin = null, Redactor? redactor = null) =>
        SnapshotText.Render(new Observation { Route = "/", Roots = [new SemanticNode { Ref = "n1", Children = nodes.ToList() }] }, redactor ?? Redactor.None, appOrigin).Split('\n');

    [Fact]
    public void Renders_test_IDs_hrefs_and_unnamed_placeholders_for_the_model()
    {
        var lines = Lines(
        [
            new SemanticNode { Ref = "n2", Role = "link", Name = "About", Attributes = new Dictionary<string, string> { ["href"] = "https://app.test/about" } },
            new SemanticNode { Ref = "n3", Role = "listitem", Text = "Alpha", TestId = "item" },
            new SemanticNode { Ref = "n4", Role = "textbox", Placeholder = "you@example.test" },
            new SemanticNode { Ref = "n5", Role = "textbox", Name = "Email", Placeholder = "ignored" },
        ]);

        Assert.Equal(" #n2 link \"About\" href=\"https://app.test/about\"", lines[1]);
        Assert.Equal(" #n3 listitem \"Alpha\" testid=\"item\"", lines[2]);
        Assert.Equal(" #n4 textbox placeholder=\"you@example.test\"", lines[3]);
        Assert.Equal(" #n5 textbox \"Email\"", lines[4]);
    }

    [Fact]
    public void Renders_a_link_target_on_the_screen_origin_as_its_whole_path_and_cuts_only_past_the_bound_with_a_trailing_ellipsis()
    {
        const string project = "http://127.0.0.1:3100/dashboard/e2e-dc3cf837/projects/404b2532-9fab-44dc-b3d0-f0a1b2c3d4e5";
        var longHref = "https://cdn.example.test/" + new string('a', 300);
        var lines = Lines(
            [Link("Project", project), Link("Docs", "https://docs.example.test/guides/setup"), Link("Search", "http://127.0.0.1:3100/search?…#…"), Link("Asset", longHref), Link("Lookalike", "http://127.0.0.1:31000/x")],
            "http://127.0.0.1:3100");

        Assert.Equal(" #n link \"Project\" href=\"/dashboard/e2e-dc3cf837/projects/404b2532-9fab-44dc-b3d0-f0a1b2c3d4e5\"", lines[1]);
        Assert.Equal(" #n link \"Docs\" href=\"https://docs.example.test/guides/setup\"", lines[2]);
        Assert.Equal(" #n link \"Search\" href=\"/search?…#…\"", lines[3]);
        Assert.Equal($" #n link \"Asset\" href=\"{longHref[..256]}…\"", lines[4]);
        Assert.Equal(" #n link \"Lookalike\" href=\"http://127.0.0.1:31000/x\"", lines[5]);
    }

    [Fact]
    public void Keeps_whole_URLs_on_a_page_off_the_app_origin_since_navigation_resolves_a_path_against_the_app()
    {
        var nodes = new[] { Link("Authorize", "https://github.com/login/oauth/authorize?…"), Link("Back to app", "https://app.test/settings") };

        var lines = Lines(nodes, "https://app.test");
        Assert.Equal(" #n link \"Authorize\" href=\"https://github.com/login/oauth/authorize?…\"", lines[1]);
        Assert.Equal(" #n link \"Back to app\" href=\"/settings\"", lines[2]);
        Assert.Contains("href=\"https://app.test/settings\"", Lines(nodes)[2], StringComparison.Ordinal);
    }

    private static IReadOnlyList<ExecutorNode> Tree(IEnumerable<SemanticNode> nodes, string? appOrigin = null, Redactor? redactor = null) =>
        global::E2E.AgentObservation.ProjectTree(nodes.ToList(), redactor ?? Redactor.None, appOrigin);

    [Fact]
    public void Never_splits_a_surrogate_pair_at_the_cut_and_bounds_the_target_an_executor_tree_carries_the_same_way()
    {
        var href = "https://cdn.example.test/" + new string('a', 230) + "😀tail";
        Assert.True(char.IsHighSurrogate(href[255]));

        Assert.Equal($" #n link \"Emoji\" href=\"{href[..255]}…\"", Lines([Link("Emoji", href)])[1]);
        Assert.Equal(href[..255] + "…", Tree([Link("Emoji", href)])[0].Attributes!["href"]);
    }

    [Fact]
    public void Gives_an_executor_tree_an_app_link_absolute_cut_no_shorter_than_the_line_cuts_its_path()
    {
        var path = "/" + new string('p', 240);
        var @long = "/" + new string('q', 300);

        var hrefs = Tree([Link("Fits", "https://app.test" + path), Link("Long", "https://app.test" + @long)], "https://app.test").Select(node => node.Attributes!["href"]);

        Assert.Equal(["https://app.test" + path, "https://app.test" + @long[..256] + "…"], hrefs);
    }

    [Fact]
    public void Keeps_an_app_link_whose_path_opens_with_a_doubled_slash_whole_so_the_model_never_reads_it_as_another_host()
    {
        const string href = "https://app.test//settings";
        var node = Link("Settings", href);

        Assert.Equal($" #n link \"Settings\" href=\"{href}\"", Lines([node], "https://app.test")[1]);
        Assert.Equal(href, Tree([node], "https://app.test")[0].Attributes!["href"]);
    }

    [Fact]
    public void Redacts_a_secret_in_a_link_target_before_cutting_it_so_no_part_of_it_survives_the_cut()
    {
        var path = "/" + new string('p', 250) + "/tok-0123456789";
        var redactor = Redactor.For([Secret.Create("token", "tok-0123456789")]);

        var line = Lines([Link("Magic", "https://app.test" + path)], "https://app.test", redactor)[1];

        Assert.Equal($" #n link \"Magic\" href=\"{redactor.Redact(path)[..256]}…\"", line);
        Assert.DoesNotContain("tok-", line, StringComparison.Ordinal);
    }

    private static SemanticNode Field(string refId, string? name = null, string? value = null, string? text = null, string? selection = null, bool focused = false, bool secure = false) =>
        new() { Ref = refId, Role = "textbox", Name = name, Value = value, Text = text, Selection = selection, States = new NodeStates { Focused = focused, Secure = secure } };

    [Fact]
    public void Renders_the_selected_text_of_a_focused_field_verbatim_after_its_value_a_lone_space_included_never_for_a_secure_one()
    {
        var lines = Lines(
        [
            Field("n2", value: "release approved", selection: "approved", focused: true),
            Field("n3", value: "two\nlines", selection: "two\nli", focused: true),
            Field("n4", name: "Password", value: "hunter2", selection: "hunter2", focused: true, secure: true),
            Field("n5", value: "caret only", selection: ""),
            Field("n6", value: "alpha beta", selection: " ", focused: true),
        ]);

        Assert.Equal(" #n2 textbox value=\"release approved\" selection=\"approved\" [focused]", lines[1]);
        // The port shows a value as it is, newline included; upstream escapes it.
        Assert.Equal(" #n3 textbox value=\"two", lines[2]);
        Assert.Equal("lines\" selection=\"two\\nli\" [focused]", lines[3]);
        Assert.Equal(" #n4 textbox \"Password\" secure [focused]", lines[4]);
        Assert.Equal(" #n5 textbox value=\"caret only\"", lines[5]);
        Assert.Equal(" #n6 textbox value=\"alpha beta\" selection=\" \" [focused]", lines[6]);
    }

    [Fact]
    public void Escapes_a_selection_as_JSON_does_including_a_lone_surrogate()
    {
        var lines = Lines([Field("n2", value: "x", selection: "a\"b\\c\u001f😀\ud83d", focused: true)]);

        Assert.Equal(" #n2 textbox value=\"x\" selection=\"a\\\"b\\\\c\\u001f😀\\ud83d\" [focused]", lines[1]);
    }

    [Fact]
    public void Withholds_the_selection_of_a_plain_field_that_holds_a_registered_secret_everywhere_the_tree_goes()
    {
        const string secret = "plain-secret-Kq7ZrT2mWx9pLd4sNv8bHc3jFg6yQa1eUo5iRk0tYw2zXn7u";
        var fragment = secret[5..45];
        var redactor = Redactor.For([Secret.Create("member", secret)]);

        var lines = Lines(
            [
                Field("n2", name: "Token", value: secret, selection: fragment, focused: true),
                Field("n3", name: "Note", value: "release approved", selection: "approved"),
                Field("n4", name: "Rich", text: $"key {secret}", selection: fragment),
            ],
            redactor: redactor);

        Assert.Equal(
            [
                " #n2 textbox \"Token\" value=\"<secret:member>\" [focused]",
                " #n3 textbox \"Note\" value=\"release approved\" selection=\"approved\"",
                " #n4 textbox \"Rich\" text=\"key <secret:member>\"",
            ],
            lines.Skip(1));
        Assert.DoesNotContain(lines, line => line.Contains(fragment, StringComparison.Ordinal));
        Assert.DoesNotContain(fragment, System.Text.Json.JsonSerializer.Serialize(Tree(
            [
                Field("n2", name: "Token", value: secret, selection: fragment, focused: true),
                Field("n3", name: "Note", value: "release approved", selection: "approved"),
                Field("n4", name: "Rich", text: $"key {secret}", selection: fragment),
            ],
            redactor: redactor)), StringComparison.Ordinal);
    }

    [Fact]
    public void Withholds_the_selection_of_a_field_cut_at_its_limit_that_may_hold_a_secret_the_cut_hides()
    {
        const string secret = "plain-secret-Kq7ZrT2mWx9pLd4sNv8bHc3jFg6yQa1eUo5iRk0tYw2zXn7u";
        var filler = string.Concat(Enumerable.Repeat("lorem ipsum. ", 50));
        string Cut(string tail) => filler[..(ObservationLimits.Text - tail.Length)] + tail;
        var redactor = Redactor.For([Secret.Create("member", secret)]);

        var text = string.Join('\n', Lines(
            [
                Field("n2", value: Cut(secret[..30]), selection: secret[5..25]),
                Field("n3", value: Cut(""), selection: secret[5..45]),
                Field("n4", value: Cut(""), selection: "ipsum"),
            ],
            redactor: redactor));

        // The value ends in the start of the secret, cut short there.
        Assert.DoesNotContain("selection=\"" + secret[5..25], text, StringComparison.Ordinal);
        Assert.Equal(1, text.Split("selection=").Length - 1);
        // Nothing shown holds a secret, but the selection is not in what is shown: it came from the cut-off rest.
        Assert.Contains("selection=\"ipsum\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain(secret[13..25], text, StringComparison.Ordinal);
    }
}
