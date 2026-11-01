// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.ReportJson;

namespace E2E.Tests.TruncatedSecrets;

/// <summary>
/// A secret the web engine's observation limits cut short is still redacted: a filled credential echoed after filler
/// so the text (512) and name (256) limits keep all but its last character, as typed or upper-cased by CSS, reaches
/// no model input, the failure screen, the report, or any file the session writes, while a plain value placed the same way
/// is still cut and shown as it is. The port fills the secret through <c>act</c>, the only way a secret joins the
/// redaction list, into a password field, the only field a secret fill accepts. It writes no <c>.e2e</c> directory, so
/// "every file under .e2e" is every file under the output directory of the session.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class SecretsCutShortByObservationLimitsTests
{
    private const string Secret = "cut-secret-Kq7ZrT2mWx9pLd4sNv8bHc3jFg6yQa1eUo5iRk0tYw2zXn7uM";

    // The shortest leading part checked for: any longer one contains it.
    private const string Fragment = "cut-secr";

    // The same part as the CSS text-transform: uppercase echo shows it.
    private const string UpperFragment = "CUT-SECR";

    // What the control paragraph keeps once the text limit cuts it.
    private const string ControlKept = "plain-control-plain-control-plain-control-plain-control-pla";

    private const string Page = """
        <!doctype html>
        <html>
        <head><title>Echo cut</title></head>
        <body>
          <label for="source">Source</label>
          <input id="source" type="password" />
          <p id="echo"></p>
          <div id="echo-upper" contenteditable="true" aria-label="Upper echo" style="text-transform: uppercase"></div>
          <button id="echo-name">Echo</button>
          <p id="control"></p>
          <script>
            const filler = (length) => '0123456789'.repeat(60).slice(0, length);
            const echo = (value) => {
              document.getElementById('echo').textContent = filler(512 - value.length + 1) + value;
              document.getElementById('echo-upper').textContent = filler(512 - value.length + 1) + value;
              document.getElementById('echo-name').setAttribute('aria-label', filler(256 - value.length + 1) + value);
            };
            document.getElementById('source').addEventListener('input', (event) => echo(event.target.value));
            document.getElementById('control').textContent = filler(512 - 59) + 'plain-control-'.repeat(5);
          </script>
        </body>
        </html>
        """;

    [Fact]
    public async Task Keeps_the_cut_fragment_out_of_the_model_input_and_still_cuts_a_plain_value()
    {
        var (model, error) = await RunAsync(report: null, tapMissingButton: false);

        Assert.Null(error);
        var observation = string.Join('\n', model.Requests.SelectMany(request => request.Messages.Select(message => message.Content)));
        Assert.Contains(ControlKept + "\"", observation, StringComparison.Ordinal);
        Assert.DoesNotContain(Fragment, observation, StringComparison.Ordinal);
        Assert.DoesNotContain(UpperFragment, observation, StringComparison.Ordinal);
        // The echoed texts and the button's name each end in the marker where the cut value stood.
        Assert.True(Regex.Matches(observation, "<secret:member>\"").Count >= 3);
    }

    [Fact]
    public async Task Keeps_the_cut_fragment_out_of_the_failure_screen_the_report_and_every_file_under_the_output_directory()
    {
        using var report = new ReportDirectory();
        var (_, error) = await RunAsync(report, tapMissingButton: true);

        Assert.Equal("LOCATOR_NOT_FOUND", Assert.IsType<TestException>(error).Code);
        var attempt = report.LastAttempt();
        Assert.Equal("LOCATOR_NOT_FOUND", (string)attempt["error"]!["code"]!);
        var screenId = (string)attempt["failure"]!["screen"]!;
        var screenArtifact = attempt["artifacts"]!.AsArray().Single(artifact => (string)artifact!["id"]! == screenId)!;
        var screenText = await File.ReadAllTextAsync(Path.Combine(report.Results, (string)screenArtifact["path"]!));
        Assert.Contains(ControlKept + "\"", screenText, StringComparison.Ordinal);
        // The echo is on the screen, as the marker where the cut value stood, once for each of the three echoes: without it the absence checks below would prove nothing.
        Assert.True(Regex.Matches(screenText, "<secret:member>\"").Count >= 3);
        var document = await File.ReadAllTextAsync(Path.Combine(report.Output, "report.json"));
        var files = Directory.EnumerateFiles(report.Output, "*", SearchOption.AllDirectories).Where(file => Path.GetExtension(file) is not (".png" or ".webm" or ".mp4")).ToList();
        foreach (var fragment in new[] { Fragment, UpperFragment })
        {
            Assert.DoesNotContain(fragment, screenText, StringComparison.Ordinal);
            Assert.DoesNotContain(fragment, document, StringComparison.Ordinal);
            foreach (var file in files)
            {
                Assert.False((await File.ReadAllTextAsync(file)).Contains(fragment, StringComparison.Ordinal), file);
            }
        }
    }

    private static async Task<(ScriptedModel Model, Exception? Error)> RunAsync(ReportDirectory? report, bool tapMissingButton)
    {
        using var site = await TinySite.StartAsync(context => TinySite.RespondAsync(context, Page));
        var model = new ScriptedModel(request =>
        {
            var last = request.Messages[^1];
            if (request.Messages[0].Content?.StartsWith("Statement:", StringComparison.Ordinal) == true)
            {
                return ModelResponses.Done("passed", "the echo is shown");
            }

            return last.Role == "tool"
                ? ModelResponses.Done("passed", "filled")
                : ModelResponses.Call("fill_secret", new { role = "textbox", name = "Source", secret = "member" });
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            Model = model,
            BaseUrl = site.Url,
            CacheEnabled = false,
            ResultsDirectory = report?.Results,
            TestTitle = "echoes a secret across the observation limits",
            AssertionTimeout = TimeSpan.FromSeconds(5),
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(20),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
        Exception? error = null;
        try
        {
            await session.Context.App.OpenAsync("/");
            await session.Context.Agent.ActAsync("fill the source with {password}", new ActOptions
            {
                Params = new Dictionary<string, object?> { ["password"] = E2E.Secret.Create("member", Secret) },
            });
            await session.Context.Agent.AssertAsync("the echo is on screen");
            if (tapMissingButton)
            {
                await session.Context.Screen.GetByRole("button", "Not on this page").TapAsync(new ActionOptions { Timeout = TimeSpan.FromMilliseconds(500) });
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        return (model, error);
    }
}
