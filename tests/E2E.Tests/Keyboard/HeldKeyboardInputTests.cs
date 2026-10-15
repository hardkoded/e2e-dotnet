// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using E2E.Engine;

namespace E2E.Tests.Keyboard;

/// <summary>Held keyboard input through the actual browser fixture and native DOM events.</summary>
[Collection(BrowserCollection.Name)]
public sealed class HeldKeyboardInputTests
{
    private const string Page = """
        <!doctype html><html><body><h1 id="events"></h1>
        <script>
          for (const type of ['keydown', 'keyup']) document.addEventListener(type, event => {
            document.querySelector('#events').textContent += `${event.type}|${event.key}|${event.shiftKey}|${event.repeat};`;
          });
        </script></body></html>
        """;

    [Fact]
    public async Task Keeps_a_modifier_held_across_another_key_and_releases_it_for_later_input()
    {
        await AttemptAsync(async session =>
        {
            await session.Browser.Keyboard.DownAsync("Shift");
            await session.Browser.Keyboard.PressAsync("KeyB");
            await session.Browser.Keyboard.UpAsync("Shift");
            await session.Browser.Keyboard.PressAsync("KeyB");
            var events = await EventsAsync(session);
            Assert.Contains("keydown|B|true|false;", events, StringComparison.Ordinal);
            Assert.Contains("keyup|Shift|false|false;", events, StringComparison.Ordinal);
            Assert.Contains("keydown|b|false|false;", events, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Marks_a_second_down_as_repeat_and_a_later_release_ends_the_held_sequence()
    {
        await AttemptAsync(async session =>
        {
            await session.Browser.Keyboard.DownAsync("a");
            await session.Browser.Keyboard.DownAsync("a");
            await session.Browser.Keyboard.UpAsync("a");
            await session.Browser.Keyboard.DownAsync("a");
            await session.Browser.Keyboard.UpAsync("a");
            Assert.Equal(
                "keydown|a|false|false;keydown|a|false|true;keyup|a|false|false;keydown|a|false|false;keyup|a|false|false;",
                await EventsAsync(session));
        });
    }

    private static async Task<string> EventsAsync(E2ESession session) =>
        await session.Browser.EvaluateAsync<string>("() => document.querySelector('#events')?.textContent ?? ''") ?? string.Empty;

    private static async Task AttemptAsync(Func<E2ESession, Task> body)
    {
        using var site = await TinySite.StartAsync(Page);
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(new WebEngineOptions { Headless = true }),
            BaseUrl = site.Url,
            TestTitle = "web > held keyboard input",
            AssertionTimeout = TimeSpan.FromSeconds(5),
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(20),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
        await using (session)
        {
            Exception? error = null;
            try
            {
                await session.App.OpenAsync("/");
                await body(session);
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
