// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using static E2E.Tests.StepCache.StepCachePipelineTests;

namespace E2E.Tests.TraceTemplate;

/// <summary>
/// The port templates the typed value, the navigated URL, the scroll text, and the start and end routes of an entry, with a
/// <c>{{param:/name}}</c> placeholder for each unique value and an encoding after a bar (<c>|uri</c>, <c>|form</c>, <c>|slug</c>,
/// <c>|flat</c>), and the upload paths, the target descriptors and the anchors. It records no action summaries and no verdict
/// summary, so the upstream it "keeps the verdict summary as recorded" is not ported. It templates each text as it records it and
/// has no <c>templateTrace</c> or <c>expandTrace</c> to call on a finished trace (issue #887 extracts them), so the tests that
/// reach a trace through them are partial.
/// </summary>
public sealed class TemplateTraceAndExpandTraceTests
{
    private static Dictionary<string, object?> Name(string value) => new() { ["name"] = Values.Unique(value) };

    [Fact]
    public void Leaves_a_trace_without_templates_untouched_and_expands_it_as_a_no_op()
    {
        const string text = "tap option \"Acme\" in row \"Acme\"";

        Assert.Equal(text, CacheKeys.Template(text, null));
        Assert.Equal(text, CacheKeys.Template(text, new Dictionary<string, object?>()));
        Assert.Equal(text, CacheKeys.Template(text, new Dictionary<string, object?> { ["name"] = "Acme" }));
        Assert.Equal(text, CacheKeys.Detemplate(text, null));
        Assert.Equal(text, CacheKeys.Detemplate(text, new Dictionary<string, object?>()));

        // A marked value the text never spelled leaves no slot; the recording is literal.
        Assert.Equal(text, CacheKeys.Template(text, Name("Globex")));
    }

    /// <summary>
    /// Partial port of "refuses to template when two marked params share a spelling": the port reports two marked params
    /// with one spelling, and does not look at a value that is another's encoded form.
    /// </summary>
    [Fact]
    public void Reports_two_marked_params_that_share_a_spelling_as_a_collision()
    {
        var shared = new Dictionary<string, object?> { ["name"] = Values.Unique("Acme Corp"), ["slug"] = Values.Unique("Acme Corp") };

        Assert.True(CacheKeys.Collides(shared));
        Assert.False(CacheKeys.Collides(Name("Acme Corp")));
    }

    /// <summary>
    /// Partial port of "reports a unique() value that another param spells, in any of its spellings, as a collision": the
    /// port compares the top-level values as text, so a value nested in another param, a number, a flag, or an encoded form is not reported.
    /// </summary>
    [Fact]
    public void Reports_a_unique_value_that_another_top_level_param_spells_exactly_as_a_collision()
    {
        // The repro: a run-unique title that happened to equal the plain choice.
        Assert.True(CacheKeys.Collides(new Dictionary<string, object?> { ["title"] = Values.Unique("Daily"), ["frequency"] = "Daily" }));

        // Distinct values do not, nor does a plain value spelled inside the marked one.
        Assert.False(CacheKeys.Collides(new Dictionary<string, object?> { ["title"] = Values.Unique("Cabin xyz"), ["frequency"] = "Daily" }));
        Assert.False(CacheKeys.Collides(new Dictionary<string, object?> { ["label"] = Values.Unique("Pine Way"), ["street"] = "Way" }));
        Assert.False(CacheKeys.Collides(new Dictionary<string, object?> { ["title"] = Values.Unique("Daily") }));

        // A multi-line value is also spelled on one line, so a plain param with that spelling collides.
        Assert.True(CacheKeys.Collides(new Dictionary<string, object?> { ["address"] = Values.Unique("Ada\nLovelace"), ["name"] = "Ada Lovelace" }));
        Assert.False(CacheKeys.Collides(new Dictionary<string, object?> { ["address"] = Values.Unique("Ada\nLovelace") }));

        // The form spelling, and a second marked value that spells the first in any spelling.
        Assert.True(CacheKeys.Collides(new Dictionary<string, object?> { ["q"] = Values.Unique("a b"), ["plus"] = "a+b" }));
        Assert.True(CacheKeys.Collides(new Dictionary<string, object?> { ["one"] = Values.Unique("a b"), ["two"] = Values.Unique("a+b") }));
        Assert.False(CacheKeys.Collides(null));
    }

    /// <summary>
    /// The port's slots are <c>{{param:/name|encoding}}</c>, not JSON-pointer placeholders, so the last upstream
    /// <c>expandText</c> check (a <c>uri</c> placeholder of an unpaired surrogate is refused) is kept as a slot left as recorded.
    /// </summary>
    [Fact]
    public void Partial_Spells_the_form_encoding_as_a_form_submission_does_and_skips_the_encodings_of_a_value_that_has_none()
    {
        Assert.Equal(
            "?q={{param:/q|form}}",
            CacheKeys.Template("?q=Ada%27s+%28new%29+shop%7E%21", new Dictionary<string, object?> { ["q"] = Values.Unique("Ada's (new) shop~!") }));

        // An unpaired surrogate cannot be percent-encoded; the value itself still templates and staging does not throw.
        var odd = "bad \ud800 value";
        var unique = new Dictionary<string, object?> { ["v"] = Values.Unique(odd) };
        Assert.Equal("typed {{param:/v}}", CacheKeys.Template("typed " + odd, unique));
        Assert.Null(CacheKeys.Detemplate("{{param:/v|uri}}", unique));
    }

    [Fact]
    public void Templates_the_location_paths_too_in_every_spelling_a_URL_gives_the_value_so_a_value_in_the_URL_follows_the_run()
    {
        var recorded = CacheKeys.Template("/companies/Acme%20%26%20Co?search=Acme+%26+Co&name=Acme & Co", Name("Acme & Co"));

        Assert.Equal("/companies/{{param:/name|uri}}?search={{param:/name|form}}&name={{param:/name}}", recorded);
        Assert.Equal("/companies/Globex%20Inc?search=Globex+Inc&name=Globex Inc", CacheKeys.Detemplate(recorded, Name("Globex Inc")));

        // A value no encoding changes has one spelling and one placeholder.
        Assert.Equal("/tags/{{param:/tag}}?q={{param:/tag}}", CacheKeys.Template("/tags/vip?q=vip", new Dictionary<string, object?> { ["tag"] = Values.Unique("vip") }));
    }

    [Fact]
    public void Spells_a_value_as_the_slug_an_app_derives_for_a_record_path_and_fills_it_back_the_same_way()
    {
        Assert.Equal(
            "/companies/{{param:/name|slug}}?q={{param:/name|uri}}",
            CacheKeys.Template("/companies/e2e-abc-company?q=E2E%20abc%20Company", Name("E2E abc Company")));
        Assert.Equal("/companies/ada-s-shop-co", CacheKeys.Detemplate("/companies/{{param:/name|slug}}", Name("Ada's Shop & Co")));

        // A value whose slug is empty has no slug spelling.
        Assert.Equal("x {{param:/v}} y", CacheKeys.Template("x -- y", new Dictionary<string, object?> { ["v"] = Values.Unique("--") }));
    }

    [Fact]
    public void Spells_a_multi_line_value_on_one_line_as_the_screen_shows_it_and_fills_it_back_the_same_way()
    {
        var address = new Dictionary<string, object?> { ["address"] = Values.Unique("Ada Lovelace\n12 Example Street\nE2E 1") };
        Assert.Equal("Saved: {{param:/address|flat}}", CacheKeys.Template("Saved: Ada Lovelace 12 Example Street E2E 1", address));
        Assert.Equal("{{param:/address}}", CacheKeys.Template("Ada Lovelace\n12 Example Street\nE2E 1", address));

        // A textarea keeps `\r\n` as `\n`, so a value joined with `\r\n` shows on one line too.
        Assert.Equal(
            "Saved: {{param:/address|flat}}",
            CacheKeys.Template(
                "Saved: Ada Lovelace 12 Example Street E2E 1",
                new Dictionary<string, object?> { ["address"] = Values.Unique("Ada Lovelace\r\n12 Example Street\r\nE2E 1") }));
        Assert.Equal(
            "Saved: Grace Hopper 7 Navy Way E2E 2",
            CacheKeys.Detemplate(
                "Saved: {{param:/address|flat}}",
                new Dictionary<string, object?> { ["address"] = Values.Unique(" Grace Hopper\n\n 7 Navy Way\tE2E 2 ") }));
    }

    /// <summary>
    /// Partial port of "templates inputs, descriptors, anchors, and action summaries, and expands them with another run's
    /// value": the typed value, the navigated URL, the target descriptor and the end anchors carry the slot and fill from the next
    /// run. The port records no action summaries, and has no <c>templateTrace</c> or <c>expandTrace</c> to call on a trace.
    /// </summary>
    [Fact]
    public async Task Partial_Templates_inputs_descriptors_and_anchors_and_expands_them_with_another_runs_value()
    {
        var typed = new List<string>();
        static string Url(string name) => "/companies/create?name=" + Uri.EscapeDataString(name).Replace("%20", "+", StringComparison.Ordinal);
        DocumentWorld World(string current) => new DocumentWorld()
            .Map("/home", page => page.Heading("Home"))
            .Map("/companies/create", page =>
            {
                page.Status("Company form");
                DocumentElement? box = null;
                box = new DocumentElement
                {
                    Role = "textbox",
                    Name = "Name",
                    Value = "",
                    OnFill = value =>
                    {
                        typed.Add(value);
                        box!.Value = value;
                    },
                };
                page.Roots.Add(box);
                page.Roots.Add(new DocumentElement { Role = "option", Name = current });
            });
        const string first = "E2E abc Company";
        const string second = "E2E xyz Company";
        static Task Form(TestContext ctx) => Expect.That(ctx.Screen.GetByRole("status", "Company form")).ToBeVisibleAsync();
        var directory = CoreTests.TempCache();
        var recorded = await StepAsync(
            World(first),
            new FileStepCache(directory),
            "/home",
            Form,
            Script(ModelResponses.Call("navigate", new { url = Url(first) }), ModelResponses.Fill("textbox", "Name", first), ModelResponses.Tap("option", first)),
            Name(first));
        Assert.Null(recorded.Error);

        var file = Assert.Single(Directory.GetFiles(directory, "*.json"));
        var entry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(file), JsonDefaults())!;
        Assert.Equal("/companies/create?name={{param:/name|form}}", entry.Actions[0].Url);
        Assert.Equal("{{param:/name}}", entry.Actions[1].Value);
        Assert.Equal("{{param:/name}}", entry.Actions[2].Name);
        Assert.Equal("option", entry.Actions[2].Role);
        Assert.Contains(entry.Appeared, anchor => anchor.Value == "{{param:/name}}");
        Assert.DoesNotContain("E2E", File.ReadAllText(file), StringComparison.Ordinal);

        typed.Clear();
        var replay = await StepAsync(World(second), new FileStepCache(directory), "/home", Form, new ScriptedModel(_ => throw new InvalidOperationException("no model call expected")), Name(second));
        Assert.Null(replay.Error);
        Assert.Equal(1, replay.Replayed);
        Assert.Equal(["E2E xyz Company"], typed);
    }

    /// <summary>
    /// Partial port of "templates upload paths, so a file named through unique() is re-resolved from each run's value": the
    /// recorded action's paths carry the slot and fill from the next run's value. The port records no action summary, and has no
    /// <c>templateTrace</c> or <c>expandTrace</c>, so the paths go through <c>Template</c> and <c>TryExpandTrace</c>.
    /// </summary>
    [Fact]
    public void Partial_Templates_upload_paths_so_a_file_named_through_unique_is_re_resolved_from_each_runs_value()
    {
        static Dictionary<string, object?> File(string value) => new() { ["file"] = Values.Unique(value) };
        var recorded = new RecordedAction
        {
            Kind = "upload",
            Role = "button",
            Name = "Attachment",
            Paths = [.. new[] { "fixtures/run-1.json", "fixtures/static.txt" }.Select(path => CacheKeys.Template(path, File("fixtures/run-1.json")))],
        };
        var entry = new CacheEntry { Actions = [recorded] };

        Assert.Equal(["{{param:/file}}", "fixtures/static.txt"], recorded.Paths);
        Assert.Equal(["fixtures/run-2.json", "fixtures/static.txt"], recorded.Paths.Select(path => CacheKeys.Detemplate(path, File("fixtures/run-2.json"))));
        Assert.True(CacheKeys.TryExpandTrace(entry, File("fixtures/run-2.json")));
        Assert.False(CacheKeys.TryExpandTrace(entry, new Dictionary<string, object?>()));
    }

    /// <summary>
    /// Partial port of "refuses to template a recording whose text already spells a placeholder": the port has no
    /// <c>templateTrace</c> to call on a finished trace, so the step records a typed value that spells one, with a marked param and
    /// with none, and writes no recording.
    /// </summary>
    [Fact]
    public async Task Partial_Refuses_to_template_a_recording_whose_text_already_spells_a_placeholder()
    {
        var world = new DocumentWorld()
            .Map("/home", page => page.Heading("Home"))
            .Map("/companies/create", page =>
            {
                page.Status("Company form");
                page.Roots.Add(new DocumentElement { Role = "textbox", Name = "Name", Value = "", OnFill = _ => { } });
            });
        static Task Form(TestContext ctx) => Expect.That(ctx.Screen.GetByRole("status", "Company form")).ToBeVisibleAsync();

        foreach (var parameters in new[] { Name("Acme Corp"), null })
        {
            var directory = CoreTests.TempCache();
            var step = await StepAsync(
                world,
                new FileStepCache(directory),
                "/home",
                Form,
                Script(ModelResponses.Call("navigate", new { url = "/companies/create" }), ModelResponses.Fill("textbox", "Name", "typed {{param:/name}} literally")),
                parameters);

            Assert.Null(step.Error);
            Assert.Empty(Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : []);
        }

        // The page itself spells one: an end anchor that could not be told from a slot.
        var spelling = new DocumentWorld()
            .Map("/home", page => page.Heading("Home"))
            .Map("/companies/create", page =>
            {
                page.Status("Company form");
                page.Heading("Saved {{param:/x}}");
            });
        var anchored = CoreTests.TempCache();
        var shown = await StepAsync(spelling, new FileStepCache(anchored), "/home", Form, Script(ModelResponses.Call("navigate", new { url = "/companies/create" })), null);

        Assert.Null(shown.Error);
        Assert.Empty(Directory.Exists(anchored) ? Directory.GetFiles(anchored, "*.json") : []);
    }

    /// <summary>No upstream <c>it</c>: upstream's <c>readTraceEntry</c> shapes the entry, and the port reads it as JSON, where a list can hold a null.</summary>
    [Fact]
    public async Task Misses_an_entry_whose_anchor_list_holds_a_null()
    {
        var world = new DocumentWorld()
            .Map("/home", page => page.Heading("Home"))
            .Map("/companies/create", page => page.Status("Company form"));
        static Task Form(TestContext ctx) => Expect.That(ctx.Screen.GetByRole("status", "Company form")).ToBeVisibleAsync();
        var directory = CoreTests.TempCache();
        var recorded = await StepAsync(world, new FileStepCache(directory), "/home", Form, Script(ModelResponses.Call("navigate", new { url = "/companies/create" })), Name("Acme"));
        Assert.Null(recorded.Error);
        var file = Assert.Single(Directory.GetFiles(directory, "*.json"));
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        json["appeared"] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null);
        File.WriteAllText(file, json.ToJsonString());

        var replay = await StepAsync(world, new FileStepCache(directory), "/home", Form, Script(ModelResponses.Call("navigate", new { url = "/companies/create" })), Name("Acme"));

        Assert.Null(replay.Error);
        Assert.Equal(("missed", "invalid-entry"), (replay.Cache?.Mode, replay.Cache?.Reason));
    }

    [Fact]
    public void Refuses_a_placeholder_whose_encoding_it_does_not_know()
    {
        Assert.Null(CacheKeys.ExpandText("/x/{{param:/name|base64}}", new Dictionary<string, string> { ["/name"] = "Acme" }));
        Assert.Null(CacheKeys.Detemplate("/x/{{param:/name|base64}}", Name("Acme")));
        Assert.Null(CacheKeys.Detemplate("/x/{{param:/other}}", Name("Acme")));
        Assert.Equal("/x/Acme", CacheKeys.Detemplate("/x/{{param:/name}}", Name("Acme")));
    }

    private static JsonSerializerOptions JsonDefaults() => new(JsonSerializerDefaults.Web);
}
