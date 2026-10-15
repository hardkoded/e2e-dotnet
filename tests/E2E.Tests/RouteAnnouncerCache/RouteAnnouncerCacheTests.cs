// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.RouteAnnouncerCache;

/// <summary>
/// Ported from upstream's <c>route-announcer-cache.test.ts</c>: record and replay around Next.js App Router's route
/// announcer in real Chromium. Upstream drives the step with an executor that taps the link; the port's step is a
/// scripted model that taps it, which records and replays the same entry. The test is partial: upstream asserts that the
/// executor ran exactly once while recording, and the port asserts only that the model was called, since a scripted
/// step makes a tap call and a done call.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class RouteAnnouncerCacheTests
{
    private const string Title = "opens Library";

    // Next.js App Router's shadow live region echoes the title on its own schedule; an app alert with the same text
    // must still gate replay. Upstream's page reads its variant from the URL; here the site injects the current one.
    private const string Page = """
        <!doctype html>
        <title>Acme Notes</title>
        <main><p data-testid="draft">Draft notes</p><a href="/library">Library</a></main>
        <next-route-announcer></next-route-announcer>
        <script>
          const variant = '__VARIANT__';
          const announcer = document.querySelector('next-route-announcer').attachShadow({ mode: 'open' });
          announcer.innerHTML = '<div id="__next-route-announcer__" role="alert" aria-live="assertive" style="position:absolute;width:1px;height:1px;overflow:hidden"></div>';
          document.querySelector('a').addEventListener('click', (event) => {
            event.preventDefault();
            history.pushState({}, '', '/library');
            document.querySelector('main').innerHTML = '<h1>Library</h1><p data-testid="notes">Meeting notes</p>' +
              (variant === 'app-alert' ? '<div role="alert" data-testid="app-alert">Acme Notes</div>' : '');
            if (variant !== 'silent') announcer.firstElementChild.textContent = document.title;
          });
        </script>
        """;

    [Theory]
    [InlineData("announced", "silent", true)]
    [InlineData("silent", "announced", true)]
    [InlineData("announced", "app-alert", false)]
    public async Task Partial_records_a_variant_with_a_scripted_tap_and_replays_another_without_counting_executor_runs(string recordVariant, string replayVariant, bool passes)
    {
        var variant = recordVariant;
        using var site = await TinySite.StartAsync(context => TinySite.RespondAsync(context, Page.Replace("__VARIANT__", variant, StringComparison.Ordinal)));
        var directory = CoreTests.TempCache();
        var calls = 0;

        await using (var recording = await StartAsync(site, directory, strict: false, () => calls++))
        {
            await OpenLibraryAsync(recording);
            recording.Complete();
        }

        Assert.True(calls > 0);
        var entry = Assert.IsType<CacheEntry>(new FileStepCache(directory).Read(Path.GetFileNameWithoutExtension(Assert.Single(Directory.GetFiles(directory, "*.json")))).Entry);
        Assert.Equal("/library", entry.EndRoute);
        Assert.Contains(entry.Appeared, anchor => anchor is { Text: "Meeting notes", TestId: "notes" });
        Assert.Contains(entry.Gone, anchor => anchor is { Text: "Draft notes", TestId: "draft" });

        calls = 0;
        variant = replayVariant;
        await using var replay = await StartAsync(site, directory, strict: true, () => calls++);
        if (passes)
        {
            var result = await OpenLibraryAsync(replay);
            replay.Complete();
            Assert.Equal(1, replay.Replayed);
            Assert.Equal("self-finalized", result.Cache?.Mode);
        }
        else
        {
            await replay.App.OpenAsync("/route-announcer");
            var error = await Assert.ThrowsAsync<AgentException>(() => replay.Agent.ActAsync("Open Library"));
            Assert.Equal("REPLAY_STALE", error.Code);
            Assert.Contains("(end-mismatch)", error.Message, StringComparison.Ordinal);
            replay.Complete(error);
        }

        Assert.Equal(0, calls);
        Assert.DoesNotContain(entry.Appeared, anchor => anchor.Role == "alert");
    }

    private static async Task<ActResult> OpenLibraryAsync(E2ESession session)
    {
        await session.App.OpenAsync("/route-announcer");
        var result = await session.Agent.ActAsync("Open Library");
        await Expect.That(session.Screen.GetByTestId("notes")).ToHaveTextAsync("Meeting notes");
        return result;
    }

    private static Task<E2ESession> StartAsync(TinySite site, string directory, bool strict, Action onAct)
    {
        return E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            Model = new ScriptedModel(request =>
            {
                onAct();
                return string.Join('\n', request.Messages.Select(message => message.Content)).Contains("tapped", StringComparison.Ordinal)
                    ? ModelResponses.Done("passed", "Library opened")
                    : ModelResponses.Tap("link", "Library");
            }),
            BaseUrl = site.Url,
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            CacheStrict = strict,
            TestTitle = Title,
            AssertionTimeout = TimeSpan.FromSeconds(5),
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(20),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
    }
}
