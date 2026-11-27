// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.Tests.FileApp.FileAppSite;

namespace E2E.Tests.FileApp;

/// <summary>Upstream's <c>describe('the agent on a file app')</c> (<c>file-app.test.ts</c>).</summary>
[Collection(BrowserCollection.Name)]
public sealed class TheAgentOnAFileAppTests
{
    [Fact]
    public async Task Gets_no_pixels_is_refused_a_file_outside_the_directory_by_URL_and_is_shown_nothing_of_one_a_link_leads_to()
    {
        var calls = new List<(string Prompt, bool Images, string LastToolResult)>();
        var model = new ScriptedModel(request =>
        {
            var turn = calls.Count + 1;
            var prompt = string.Join('\n', request.Messages.Select(message => message.Content ?? ""));
            var last = request.Messages.LastOrDefault(message => message.Role == "tool")?.Content ?? "";
            calls.Add((prompt, request.Messages.Any(message => message.Images?.Count > 0), last));
            return turn switch
            {
                1 => ModelResponses.Call("screenshot", new { }),
                2 => ModelResponses.Call("navigate", new { url = "../secret.html" }),
                3 => ModelResponses.Call("tap", new { target = NodeIdFor(prompt, "link \"Notes\"") }),
                _ => ModelResponses.Done("passed", "read the notes"),
            };
        });
        var project = CreateProject();
        try
        {
            var outcome = await RunProject.RunExistingAsync(
                project,
                Options(project, [new("the agent cannot be led out of the directory", async session =>
                        {
                            await session.App.OpenAsync();
                            await session.Agent.ActAsync("Read the notes");
                        })], new Dictionary<string, AgentOptions> { ["default"] = new() { Model = model } })
            );

            Assert.Equal(4, calls.Count);
            // A file app's pixels can show a frame of another local file, so the model is sent none, even on request.
            Assert.All(calls, call => Assert.False(call.Images));
            // The typed URL is the navigation rule's: the model reads the refusal and the page has not moved.
            Assert.Contains("POLICY_DENIED", calls[2].LastToolResult, StringComparison.Ordinal);
            Assert.Contains(
                "stays in the directory of the app's file",
                calls[2].LastToolResult,
                StringComparison.Ordinal
            );
            // The link is the page's own, so the browser follows it; what it shows there is refused in place of the screen.
            Assert.Contains(
                "a file app stays in the directory of the app's file",
                calls[3].LastToolResult,
                StringComparison.Ordinal
            );
            Assert.Single(RunProject.ResultByTitle(outcome, "the agent cannot be led out of the directory").Attempts);
            foreach (var call in calls)
            {
                Assert.DoesNotContain(
                    "TOP-SECRET-NOTES",
                    call.Prompt + "\n" + call.LastToolResult,
                    StringComparison.Ordinal
                );
            }
        }
        finally
        {
            project.Cleanup();
        }
    }

    private static string NodeIdFor(string prompt, string pattern) =>
        Regex
            .Matches(
                prompt,
                @"^\s*(?:added |changed )?#(e\d+) " + Regex.Escape(pattern) + @"[^\n]*$",
                RegexOptions.Multiline
            )
            .Select(match => match.Groups[1].Value)
            .LastOrDefault()
        ?? throw new InvalidOperationException("No line " + pattern + " in the screen:\n" + prompt);
}
