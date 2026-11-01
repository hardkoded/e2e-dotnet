// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Tests.StepCache.StepCachePipelineTests;

namespace E2E.Tests.TraceTemplate;

/// <summary>
/// Upstream's <c>templateTrace</c> and <c>expandTrace</c> are <see cref="CacheKeys.TemplateTrace"/> and <see cref="CacheKeys.ExpandTrace"/>,
/// called on a <see cref="CacheEntry"/>. The port records no action summaries and no verdict summary, and its actions hold no
/// <c>within</c>, so the checks of those fields are not ported; a test that needs one is partial.
/// </summary>
public sealed class TemplateTraceAndExpandTraceTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static List<ParamTemplate> Name(string value) => [new("/name", value)];

    private static Dictionary<string, object?> NameParams(string value) => new() { ["name"] = Values.Unique(value) };

    private static Dictionary<string, string> Filled(IEnumerable<ParamTemplate> templates) => templates.ToDictionary(template => template.Pointer, template => template.Value);

    /// <summary>The entry as JSON, so two entries compare by what they hold.</summary>
    private static string Json(CacheEntry? entry) => JsonSerializer.Serialize(entry, Web);

    /// <summary>Upstream's <c>trace(name)</c> of <c>trace-template.test.ts</c>, as the port records it.</summary>
    private static CacheEntry Recording(string name) => new()
    {
        Actions =
        [
            new RecordedAction { Kind = "navigate", Url = "/companies/create" },
            new RecordedAction { Kind = "fill", Role = "textbox", Name = "Name", Value = name },
            new RecordedAction { Kind = "fill", Role = "textbox", Name = "Password", Value = "<secret:admin>" },
            new RecordedAction { Kind = "tap", Role = "option", Name = name },
            new RecordedAction { Kind = "press", Role = "textbox", Name = "Name", Key = "Enter" },
        ],
        Route = "/companies",
        EndRoute = "/companies/123",
        Appeared = [new RecordedTarget { Role = "heading", Name = name }],
    };

    /// <summary>
    /// Partial port of "templates upload paths, so a file named through unique() is re-resolved from each run's value": the recorded
    /// action's paths carry the slot and fill from the next run's value. The port records no action summary, so the summary check is
    /// not ported.
    /// </summary>
    [Fact]
    public void Partial_Templates_upload_paths_so_a_file_named_through_unique_is_re_resolved_from_each_runs_value()
    {
        static CacheEntry Upload(string file) => new()
        {
            Actions = [new RecordedAction { Kind = "upload", Role = "button", Name = "Attachment", Paths = [file, "fixtures/static.txt"] }],
            Route = "/",
        };
        static List<ParamTemplate> Paths(string value) => [new("/file", value)];

        var recorded = CacheKeys.TemplateTrace(Upload("fixtures/run-1.json"), Paths("fixtures/run-1.json"))!;

        Assert.Equal(["{{param:/file}}", "fixtures/static.txt"], recorded.Actions[0].Paths);
        Assert.Equal(Json(Upload("fixtures/run-2.json")), Json(CacheKeys.ExpandTrace(recorded, Paths("fixtures/run-2.json"))));
        Assert.Null(CacheKeys.ExpandTrace(recorded, []));
    }

    /// <summary>
    /// Partial port of "templates inputs, descriptors, anchors, and action summaries, and expands them with another run's value": the
    /// typed value, the target descriptor and the end anchors carry the slot and fill from the next run. The port records no action
    /// summaries and no verdict summary, and its actions hold no <c>within</c>, so those checks are not ported.
    /// </summary>
    [Fact]
    public void Partial_Templates_inputs_descriptors_anchors_and_action_summaries_and_expands_them_with_another_runs_value()
    {
        var recorded = CacheKeys.TemplateTrace(Recording("E2E abc Company"), Name("E2E abc Company"))!;

        Assert.Equal("{{param:/name}}", recorded.Actions[1].Value);
        Assert.Equal("{{param:/name}}", recorded.Actions[3].Name);
        Assert.Equal("option", recorded.Actions[3].Role);
        Assert.Equal("{{param:/name}}", Assert.Single(recorded.Appeared).Name);
        Assert.Equal("heading", recorded.Appeared[0].Role);

        // Secrets and keys are never text a parameter produced.
        Assert.Equal("<secret:admin>", recorded.Actions[2].Value);
        Assert.Equal("Enter", recorded.Actions[4].Key);

        Assert.Equal(Json(Recording("E2E xyz Company")), Json(CacheKeys.ExpandTrace(recorded, Name("E2E xyz Company"))));
        Assert.Null(CacheKeys.ExpandTrace(recorded, [new("/title", "E2E xyz Company")]));
        Assert.Null(CacheKeys.ExpandTrace(recorded, []));
    }

    [Fact]
    public void Leaves_a_trace_without_templates_untouched_and_expands_it_as_a_no_op()
    {
        var plain = Recording("Acme");

        Assert.Equal(Json(plain), Json(CacheKeys.TemplateTrace(plain, [])));
        Assert.Equal(Json(plain), Json(CacheKeys.ExpandTrace(plain, [])));

        // A marked value the text never spelled leaves no slot; the recording is literal.
        Assert.Equal(Json(plain), Json(CacheKeys.TemplateTrace(plain, Name("Globex"))));
    }

    /// <summary>
    /// Upstream spells the placeholder in the verdict summary; the port has none, so the entry spells it in a typed value, which
    /// <c>mapTraceText</c> maps as well.
    /// </summary>
    [Fact]
    public void Refuses_to_template_a_recording_whose_text_already_spells_a_placeholder()
    {
        var odd = Recording("Acme Corp");
        odd.Actions[1].Value = "typed {{param:/name}} literally";

        Assert.Null(CacheKeys.TemplateTrace(odd, Name("Acme Corp")));
        Assert.Null(CacheKeys.TemplateTrace(odd, []));
    }

    [Fact]
    public void Refuses_to_template_when_two_marked_params_share_a_spelling_since_the_text_cannot_say_which_one_it_spelled()
    {
        List<ParamTemplate> shared = [.. Name("Acme Corp"), new("/slug", "Acme Corp")];
        Assert.Null(CacheKeys.TemplateTrace(Recording("Acme Corp"), shared));

        // One value is the other's encoded form.
        List<ParamTemplate> aliased = [.. Name("Acme Corp"), new("/slug", "Acme%20Corp")];
        Assert.Null(CacheKeys.TemplateTrace(Recording("Acme Corp"), aliased));
    }

    private static bool Collide(string json, params (string Pointer, string Value)[] templates) =>
        CacheKeys.TemplatesCollide(JsonNode.Parse(json)!.AsObject(), [.. templates.Select(template => new ParamTemplate(template.Pointer, template.Value))]);

    [Fact]
    public void Reports_a_unique_value_that_another_param_spells_in_any_of_its_spellings_as_a_collision()
    {
        (string, string)[] title = [("/title", "Daily")];

        // The repro: a run-unique title that happened to equal the plain choice.
        Assert.True(Collide("""{"title":"Daily","frequency":"Daily"}""", title));

        // Spelled inside a plain value, at any depth, and as a number.
        Assert.True(Collide("""{"title":"Daily","plan":{"label":"Daily digest"}}""", title));
        Assert.True(Collide("""{"id":"42","tags":[7,42]}""", ("/id", "42")));

        // A plain value that is the encoded form of the marked one, and the other way round: a URL
        // spells the plain choice `a b` as `a%20b`, which a marked `a%20b` would claim.
        Assert.True(Collide("""{"q":"a b","path":"/x/a%20b"}""", ("/q", "a b")));
        Assert.True(Collide("""{"title":"a%20b","choice":"a b"}""", ("/title", "a%20b")));
        Assert.True(Collide("""{"title":"a+b","choice":"a b"}""", ("/title", "a+b")));
        Assert.True(Collide("""{"title":"acme-co","name":"Acme Co"}""", ("/title", "acme-co")));

        // A boolean flag spelled in a URL as `true`.
        Assert.True(Collide("""{"title":"true","enabled":true}""", ("/title", "true")));
        Assert.False(Collide("""{"title":"true","enabled":false,"note":null}""", ("/title", "true")));

        // Two marked params with one spelling collide as before.
        Assert.True(Collide("""{"name":"Acme","slug":"Acme"}""", ("/name", "Acme"), ("/slug", "Acme")));

        // Distinct values do not, nor does a plain value spelled inside the marked one.
        Assert.False(Collide("""{"title":"Cabin xyz","frequency":"Daily"}""", ("/title", "Cabin xyz")));
        Assert.False(Collide("""{"label":"Pine Way","street":"Way"}""", ("/label", "Pine Way")));
        Assert.False(Collide("""{"title":"Daily"}""", title));
        Assert.False(CacheKeys.TemplatesCollide(null, []));
    }

    [Fact]
    public void Spells_the_form_encoding_as_a_form_submission_does_and_skips_the_encodings_of_a_value_that_has_none()
    {
        Assert.Equal("?q={{param:/q|form}}", CacheKeys.TemplateText("?q=Ada%27s+%28new%29+shop%7E%21", [new("/q", "Ada's (new) shop~!")]));

        // An unpaired surrogate cannot be percent-encoded; the value itself still templates and staging does not throw.
        var odd = "bad \ud800 value";
        Assert.Equal("typed {{param:/v}}", CacheKeys.TemplateText("typed " + odd, [new("/v", odd)]));
        Assert.Null(CacheKeys.ExpandText("{{param:/v|uri}}", new Dictionary<string, string> { ["/v"] = odd }));
    }

    [Fact]
    public void Templates_the_location_paths_too_in_every_spelling_a_URL_gives_the_value_so_a_value_in_the_URL_follows_the_run()
    {
        var moved = Recording("Acme & Co");
        moved.EndRoute = "/companies/Acme%20%26%20Co?search=Acme+%26+Co&name=Acme & Co";

        var recorded = CacheKeys.TemplateTrace(moved, Name("Acme & Co"))!;

        Assert.Equal("/companies/{{param:/name|uri}}?search={{param:/name|form}}&name={{param:/name}}", recorded.EndRoute);
        Assert.Equal("/companies/Globex%20Inc?search=Globex+Inc&name=Globex Inc", CacheKeys.ExpandTrace(recorded, Name("Globex Inc"))?.EndRoute);

        // A value no encoding changes has one spelling and one placeholder.
        Assert.Equal("/tags/{{param:/tag}}?q={{param:/tag}}", CacheKeys.TemplateText("/tags/vip?q=vip", [new("/tag", "vip")]));
    }

    [Fact]
    public void Spells_a_value_as_the_slug_an_app_derives_for_a_record_path_and_fills_it_back_the_same_way()
    {
        List<ParamTemplate> templates = [new("/name", "E2E abc Company")];

        Assert.Equal("/companies/{{param:/name|slug}}?q={{param:/name|uri}}", CacheKeys.TemplateText("/companies/e2e-abc-company?q=E2E%20abc%20Company", templates));
        Assert.Equal("/companies/ada-s-shop-co", CacheKeys.ExpandText("/companies/{{param:/name|slug}}", Filled([new("/name", "Ada's Shop & Co")])));

        // A value whose slug is empty has no slug spelling.
        Assert.Equal("x {{param:/v}} y", CacheKeys.TemplateText("x -- y", [new("/v", "--")]));
    }

    [Fact]
    public void Spells_a_multi_line_value_on_one_line_as_the_screen_shows_it_and_fills_it_back_the_same_way()
    {
        List<ParamTemplate> templates = [new("/address", "Ada Lovelace\n12 Example Street\nE2E 1")];

        Assert.Equal("Saved: {{param:/address|flat}}", CacheKeys.TemplateText("Saved: Ada Lovelace 12 Example Street E2E 1", templates));
        Assert.Equal("{{param:/address}}", CacheKeys.TemplateText("Ada Lovelace\n12 Example Street\nE2E 1", templates));

        // A textarea keeps `\r\n` as `\n`, so a value joined with `\r\n` shows on one line too.
        Assert.Equal(
            "Saved: {{param:/address|flat}}",
            CacheKeys.TemplateText("Saved: Ada Lovelace 12 Example Street E2E 1", [new("/address", "Ada Lovelace\r\n12 Example Street\r\nE2E 1")]));
        Assert.Equal(
            "Saved: Grace Hopper 7 Navy Way E2E 2",
            CacheKeys.ExpandText("Saved: {{param:/address|flat}}", Filled([new("/address", " Grace Hopper\n\n 7 Navy Way\tE2E 2 ")])));
    }

    [Fact]
    public void Refuses_a_placeholder_whose_encoding_it_does_not_know()
    {
        Assert.Null(CacheKeys.ExpandText("/x/{{param:/name|base64}}", Filled(Name("Acme"))));
        Assert.Equal("/x/Acme", CacheKeys.ExpandText("/x/{{param:/name}}", Filled(Name("Acme"))));
    }

    /// <summary>
    /// No upstream <c>it</c>: upstream's step-cache tests run a step through <c>templateTrace</c> and <c>expandTrace</c> with a real model.
    /// This one runs the port's step cache with a scripted model: the recording holds the slots, and the next run fills them.
    /// </summary>
    [Fact]
    public async Task Records_a_step_with_the_slots_and_replays_it_with_another_runs_value()
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
            NameParams(first));
        Assert.Null(recorded.Error);

        var file = Assert.Single(Directory.GetFiles(directory, "*.json"));
        var entry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(file), Web)!;
        Assert.Equal("/companies/create?name={{param:/name|form}}", entry.Actions[0].Url);
        Assert.Equal("{{param:/name}}", entry.Actions[1].Value);
        Assert.Equal("{{param:/name}}", entry.Actions[2].Name);
        Assert.Equal("option", entry.Actions[2].Role);
        Assert.Contains(entry.Appeared, anchor => anchor.Value == "{{param:/name}}");
        Assert.DoesNotContain("E2E", File.ReadAllText(file), StringComparison.Ordinal);

        typed.Clear();
        var replay = await StepAsync(World(second), new FileStepCache(directory), "/home", Form, new ScriptedModel(_ => throw new InvalidOperationException("no model call expected")), NameParams(second));
        Assert.Null(replay.Error);
        Assert.Equal(1, replay.Replayed);
        Assert.Equal(["E2E xyz Company"], typed);
    }

    /// <summary>
    /// No upstream <c>it</c>: the page itself spells a placeholder, so an end anchor could not be told from a slot. Upstream's
    /// <c>templateTrace</c> refuses it for any mapped text; this runs the port's step cache to see that it writes no recording.
    /// </summary>
    [Fact]
    public async Task Writes_no_recording_for_a_step_whose_page_spells_a_placeholder()
    {
        static Task Form(TestContext ctx) => Expect.That(ctx.Screen.GetByRole("status", "Company form")).ToBeVisibleAsync();
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
        var recorded = await StepAsync(world, new FileStepCache(directory), "/home", Form, Script(ModelResponses.Call("navigate", new { url = "/companies/create" })), NameParams("Acme"));
        Assert.Null(recorded.Error);
        var file = Assert.Single(Directory.GetFiles(directory, "*.json"));
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        json["appeared"] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null);
        File.WriteAllText(file, json.ToJsonString());

        var replay = await StepAsync(world, new FileStepCache(directory), "/home", Form, Script(ModelResponses.Call("navigate", new { url = "/companies/create" })), NameParams("Acme"));

        Assert.Null(replay.Error);
        Assert.Equal(("missed", "invalid-entry"), (replay.Cache?.Mode, replay.Cache?.Reason));
    }
}
