// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using E2E;
using E2E.Engine;

namespace E2E.Tests;

/// <summary>
/// <c>agent.act</c> grammar verbs on the gestures fixture page, driven by a scripted
/// model, then replayed with no model call. Ports the check flow of upstream
/// <c>agent-act-verbs.test.ts</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class AgentActVerbsTests
{
    // The part of upstream's /gestures page the check flow reads: the status and
    // a radio the pick replaces with its summary.
    private const string GesturesPage = """
        <!doctype html>
        <html>
        <head><title>Gestures</title></head>
        <body style="margin:0;padding:90px 16px 16px">
          <h1>Gestures</h1>
          <output id="state" role="status" aria-label="Gesture state">idle</output>

          <fieldset id="delivery"><legend>Delivery</legend><label><input type="radio" name="delivery" value="Express" />Express</label></fieldset>
          <script>
            const state = document.getElementById('state');
            const delivery = document.getElementById('delivery');
            delivery.addEventListener('change', (event) => {
              delivery.innerHTML = '<p>' + event.target.value + ' delivery selected</p>';
              state.textContent = 'delivery: ' + event.target.value;
            });
          </script>
        </body>
        </html>
        """;

    [Fact]
    public async Task Records_a_check_whose_radio_the_pick_replaced_the_click_landed()
    {
        using var site = await TinySite.StartAsync(GesturesPage);
        var directory = Path.Combine(Path.GetTempPath(), "e2e-verbs", Guid.NewGuid().ToString("n"));
        var model = CheckExpressModel();
        var session = await StartAsync(site, model, new FileStepCache(directory));
        await RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/gestures");
            await session.Agent.ActAsync("pick Express delivery");
            await Expect.That(session.Screen.GetByRole("status", "Gesture state")).ToHaveValueAsync("delivery: Express");
        });

        var actions = RecordedActions(directory);
        Assert.Equal(["check radio Express"], actions.Select(action => action.Kind + " " + action.Role + " " + action.Name));
        var turn = string.Join('\n', model.Requests[1].Messages.Select(message => message.Content));
        Assert.Matches(new Regex("""checked radio "Express"[\s\S]*status "Gesture state"[\s\S]*delivery: Express"""), turn);
    }

    [Fact]
    public async Task Replays_a_check_whose_radio_the_pick_replaced_with_zero_turns()
    {
        using var site = await TinySite.StartAsync(GesturesPage);
        var directory = Path.Combine(Path.GetTempPath(), "e2e-verbs", Guid.NewGuid().ToString("n"));
        for (var run = 0; run < 2; run++)
        {
            var model = CheckExpressModel();
            var session = await StartAsync(site, model, new FileStepCache(directory));
            await RunAsync(session, async () =>
            {
                await session.App.OpenAsync("/gestures");
                await session.Agent.ActAsync("pick Express delivery");
                await Expect.That(session.Screen.GetByRole("status", "Gesture state")).ToHaveValueAsync("delivery: Express");
            });
            Assert.Equal(run, session.Replayed);
            Assert.Equal(run == 0, model.CallCount > 0);
        }

        Assert.Equal(["check"], RecordedActions(directory).Select(action => action.Kind));
    }

    // Checks the Express radio on the first call, then reports done.
    private static ScriptedModel CheckExpressModel()
    {
        var calls = 0;
        return new ScriptedModel(request =>
        {
            if (calls++ > 0)
            {
                return ModelResponses.Done("passed", "picked Express delivery");
            }

            var prompt = string.Join('\n', request.Messages.Select(message => message.Content));
            var target = Regex.Match(prompt, @"radio ""Express"" \[ref=(e\d+)\]").Groups[1].Value;
            return ModelResponses.Call("check", new { @ref = target });
        });
    }

    private static List<RecordedAction> RecordedActions(string directory)
    {
        var entry = Assert.Single(Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories));
        using var document = JsonDocument.Parse(File.ReadAllText(entry));
        return document.RootElement.EnumerateObject()
            .First(property => string.Equals(property.Name, "actions", StringComparison.OrdinalIgnoreCase))
            .Value.Deserialize<List<RecordedAction>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static async Task<E2ESession> StartAsync(TinySite site, ScriptedModel model, FileStepCache cache)
    {
        return await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            Model = model,
            BaseUrl = site.Url,
            Cache = cache,
            CacheEnabled = true,
            TestTitle = "agent.act grammar verbs > checks a radio the pick replaces with its summary",
            AssertionTimeout = TimeSpan.FromSeconds(5),
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(20),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
    }

    private static async Task RunAsync(E2ESession session, Func<Task> body)
    {
        await using (session)
        {
            Exception? error = null;
            try
            {
                await body();
            }
            catch (Exception ex)
            {
                error = ex;
            }

            session.Complete(error);
            Assert.Null(error);
        }
    }
}
