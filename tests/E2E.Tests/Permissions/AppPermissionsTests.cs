// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Permissions;

/// <summary>
/// A web target's <c>app.permissions</c> in a real browser: headless Chromium refuses the clipboard to a page unless the target grants it,
/// and a grant holds on every context the attempt opens. Upstream drives the engine hooks as the attempt executor does; the port starts
/// an <see cref="E2ESession"/> with the target's permissions, the way the fixture does.
/// </summary>
public sealed class AppPermissionsTests
{
    [Fact]
    public Task Gives_every_page_the_clipboard_the_target_grants_after_a_restart_and_a_context_reset_too() => WithBrowserAsync(
        new Dictionary<string, AppPermissionState> { ["clipboard-read"] = AppPermissionState.Grant, ["clipboard-write"] = AppPermissionState.Grant },
        async session =>
        {
            Assert.Equal("first", await CopyAsync(session, "first"));
            await session.App.RestartAsync();
            Assert.Equal("second", await CopyAsync(session, "second"));
            await session.App.ClearStateAsync();
            Assert.Equal("third", await CopyAsync(session, "third"));
        });

    [Fact]
    public Task Leaves_the_clipboard_refused_when_the_target_grants_nothing() => WithBrowserAsync(
        null,
        async session => Assert.Matches("NotAllowedError", await CopyAsync(session, "denied")));

    /// <summary>Opens the app and copies <paramref name="text"/> through the async clipboard, reading it back: the text, or the error the page got.</summary>
    private static async Task<string> CopyAsync(E2ESession session, string text)
    {
        await session.App.OpenAsync("/");
        return (await session.Browser.EvaluateAsync<string>(
            "async (value) => { try { await navigator.clipboard.writeText(value); return await navigator.clipboard.readText(); } catch (error) { return String(error); } }",
            text))!;
    }

    /// <summary>Boots one engine into a running attempt for a target granting <paramref name="permissions"/>.</summary>
    private static async Task WithBrowserAsync(IReadOnlyDictionary<string, AppPermissionState>? permissions, Func<E2ESession, Task> body)
    {
        using var site = await TinySite.StartAsync("<!doctype html><title>Permissions</title>");
        var projectRoot = Directory.CreateTempSubdirectory("e2e-permissions-").FullName;
        try
        {
            var session = await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = new WebEngine(new WebEngineOptions { Headless = true }),
                BaseUrl = site.Url,
                Permissions = permissions,
                TargetName = "web",
                ProjectRoot = projectRoot,
                TestTitle = "app permissions",
                ActionTimeout = TimeSpan.FromSeconds(10),
                TestTimeout = TimeSpan.FromSeconds(60),
            });
            await WebEngineTests.RunAsync(session, () => body(session));
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }
}
