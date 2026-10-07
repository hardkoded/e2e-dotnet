// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using E2E;
using E2E.Engine;

namespace E2E.Tests.AgentActVerbs;

/// <summary>
/// The gestures fixture page, a scripted model, and a session on them, shared by the
/// <c>agent.act</c> grammar verb tests ported from upstream <c>agent-act-verbs.test.ts</c>.
/// </summary>
internal static class GesturesSession
{
    // The part of upstream's /gestures page the check flow reads: the status and
    // a radio the pick replaces with its summary.
    public const string GesturesPage = """
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

    // Checks the Express radio on the first call, then reports done.
    public static ScriptedModel CheckExpressModel()
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

    public static List<RecordedAction> RecordedActions(string directory)
    {
        var entry = Assert.Single(Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories));
        using var document = JsonDocument.Parse(File.ReadAllText(entry));
        return document.RootElement.EnumerateObject()
            .First(property => string.Equals(property.Name, "actions", StringComparison.OrdinalIgnoreCase))
            .Value.Deserialize<List<RecordedAction>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    public static async Task<E2ESession> StartAsync(TinySite site, ScriptedModel model, FileStepCache cache)
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

    public static async Task RunAsync(E2ESession session, Func<Task> body)
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
