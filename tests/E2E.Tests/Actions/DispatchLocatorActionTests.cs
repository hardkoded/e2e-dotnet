// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.UploadActions;
using static E2E.Tests.CheckActions.CheckPage;

namespace E2E.Tests.Actions;

/// <summary>
/// Partial ports of upstream "dispatches %j to locator.%s" (clear, selectOption label) and the node swipe: the upstream tests check call arguments and a wheel on stubs. The port checks the
/// effect on a live page, since the page calls are made inside the engine. <c>longPress</c>, the <c>index</c> form of
/// <c>selectOption</c>, <c>dragTo</c>, and the node swipe's missing-box case are not ported: the port has no long press,
/// no index select, no <c>dragTo</c>, and scrolls a node with <c>scrollBy</c>, which needs no box (COMPATIBILITY.md).
/// The five upload <c>it</c>s (upstream #958) run on a live page as well: the first two are <c>Partial_</c>, since they check the
/// order and the arguments of stubbed page calls, and the others check the same outcome the stubs do.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class DispatchLocatorActionTests
{
    [Theory]
    [InlineData("clear")]
    [InlineData("selectOption label")]
    public async Task Changes_the_field_or_the_selected_option_as_the_action_says(string action)
    {
        await RunAsync(
            """
            <input aria-label="Note" value="draft">
            <select aria-label="Color"><option>Red</option><option>Blue</option></select>
            """,
            async session =>
            {
                if (action == "clear")
                {
                    await session.PerformAsync(await FindByNameAsync(session, "Note"), new LocatorAction.Clear(), CancellationToken.None);
                    Assert.Equal("", await EvaluateAsync<string>(session, "() => document.querySelector('input').value"));
                }
                else
                {
                    await session.PerformAsync(await FindByNameAsync(session, "Color"), new LocatorAction.Select("Blue"), CancellationToken.None);
                    Assert.Equal("Blue", await EvaluateAsync<string>(session, "() => document.querySelector('select').value"));
                }
            });
    }

    [Fact]
    public async Task Scrolls_a_node_by_three_quarters_of_its_own_box()
    {
        // Partial: upstream wheels over the node by half its box, with a momentum option; the port calls scrollBy by three quarters.
        await RunAsync(
            """
            <div role="region" aria-label="Box" tabindex="0" style="width:400px;height:300px;overflow:auto">
              <div style="width:2000px;height:2000px"></div>
            </div>
            """,
            async session =>
            {
                var box = await FindByNameAsync(session, "Box");
                async Task<string> OffsetAsync() => await EvaluateAsync<string>(session, "() => document.querySelector('div').scrollLeft + ',' + document.querySelector('div').scrollTop");

                await session.PerformAsync(box, new LocatorAction.Swipe(ScrollDirection.Down), CancellationToken.None);
                Assert.Equal("0,225", await OffsetAsync());

                await session.PerformAsync(box, new LocatorAction.Swipe(ScrollDirection.Right), CancellationToken.None);
                Assert.Equal("300,225", await OffsetAsync());

                await session.PerformAsync(box, new LocatorAction.Swipe(ScrollDirection.Left), CancellationToken.None);
                Assert.Equal("0,225", await OffsetAsync());
            });
    }

    [Fact]
    public async Task Partial_Uploads_through_a_file_chooser_it_awaits_before_clicking_a_button()
    {
        // Partial: upstream stubs the page and checks the order of the chooser wait, the click, and the file set, and that the element's
        // own setInputFiles is never called. The port cannot see calls inside the engine: the button's click opens the chooser at once,
        // so the chooser must have been awaited first for the files to land, and the button is clicked once.
        await RunAsync(
            $"""
            <button type="button" onclick="document.getElementById('avatar').click()">Choose avatar</button>
            <input id="avatar" type="file" hidden>{CountClicks}
            """,
            async session =>
            {
                await session.PerformAsync(await FindByNameAsync(session, "Choose avatar"), new LocatorAction.SetInputFiles([UploadPage.AvatarFile]), CancellationToken.None);
                Assert.Equal("avatar.png", await EvaluateAsync<string>(session, "() => document.getElementById('avatar').files[0].name"));
                Assert.Equal(2, await EvaluateAsync<int>(session, "() => window.clicks"));
            });
    }

    [Fact]
    public async Task Partial_Sets_a_file_input_directly()
    {
        // Partial: upstream checks the element's setInputFiles call and that no click or chooser wait happened. The port checks the page saw no click.
        await RunAsync(
            $"""<input id="avatar" type="file" aria-label="Avatar">{CountClicks}""",
            async session =>
            {
                await session.PerformAsync(await FindByNameAsync(session, "Avatar"), new LocatorAction.SetInputFiles([UploadPage.AvatarFile]), CancellationToken.None);
                Assert.Equal("avatar.png", await EvaluateAsync<string>(session, "() => document.getElementById('avatar').files[0].name"));
                Assert.Equal(0, await EvaluateAsync<int>(session, "() => window.clicks"));
            });
    }

    [Fact]
    public async Task Fails_as_NOT_ACTIONABLE_when_the_click_opens_no_file_chooser()
    {
        await RunAsync(
            """<button type="button">Choose avatar</button>""",
            async session =>
            {
                var pick = await FindByNameAsync(session, "Choose avatar");
                var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(pick, new LocatorAction.SetInputFiles([UploadPage.AvatarFile]), CancellationToken.None));
                Assert.Equal("NOT_ACTIONABLE", error.Code);
                Assert.Contains("opened no file chooser", error.Message, StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task Sets_no_files_when_the_operation_is_cancelled_while_the_chooser_is_pending()
    {
        // The chooser opens 1.2 s after the click, well after the cancel and inside the action budget.
        await RunAsync(
            """
            <button type="button" onclick="setTimeout(() => document.getElementById('avatar').click(), 1200)">Choose avatar</button>
            <input id="avatar" type="file" hidden>
            """,
            async session =>
            {
                var pick = await FindByNameAsync(session, "Choose avatar");
                using var cancel = new CancellationTokenSource();
                var upload = session.PerformAsync(pick, new LocatorAction.SetInputFiles([UploadPage.AvatarFile]), cancel.Token);
                await Task.Delay(TimeSpan.FromMilliseconds(300));
                await cancel.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload);

                await Task.Delay(TimeSpan.FromMilliseconds(1500));
                Assert.Equal(0, await EvaluateAsync<int>(session, "() => document.getElementById('avatar').files.length"));
            });
    }

    [Fact]
    public async Task Reports_a_failed_click_without_leaving_the_chooser_wait_unhandled()
    {
        // A disabled button never becomes clickable, so the click fails while the chooser wait is still pending.
        var unobserved = new List<string>();
        void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs args) => unobserved.Add(args.Exception.ToString());
        TaskScheduler.UnobservedTaskException += OnUnobserved;
        try
        {
            await RunAsync(
                """<button type="button" disabled>Choose avatar</button>""",
                async session =>
                {
                    var pick = await FindByNameAsync(session, "Choose avatar");
                    var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(pick, new LocatorAction.SetInputFiles([UploadPage.AvatarFile]), CancellationToken.None));
                    Assert.Equal("NOT_ACTIONABLE", error.Code);

                    // Let the chooser wait fail on its own timeout, then collect it: an unobserved fault surfaces from the finalizer.
                    await Task.Delay(TimeSpan.FromSeconds(1));
                });
            for (var round = 0; round < 3; round++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
                await Task.Delay(100);
            }
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
        }

        Assert.DoesNotContain(unobserved, text => text.Contains("filechooser", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<SemanticNode> FindByNameAsync(IEngineSession session, string name)
    {
        var observation = await session.ObserveAsync(CancellationToken.None);
        return Flatten(observation.Roots).First(node => node.Name == name);
    }
}
