// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Dialog;

/// <summary>
/// <c>test.describe('page dialog')</c> of upstream's <c>apps/testbed/tests/dialog.e2e.ts</c> (Playwright's
/// <c>page-dialog.spec.ts</c>). <c>page.on('dialog')</c> is <c>Browser.OnDialogAsync</c>. A dialog no handler answers is dismissed as
/// in Playwright, and e2e also fails the next step for it. Not ported, as upstream: should fire dialogclosed when dialog is accepted,
/// should fire dialogclosed when dialog is dismissed, should fire dialogclosed for auto-dismissed dialogs (no dialogclosed event),
/// should be able to close context with open alert (e2e answers every dialog). The <c>defaultValue()</c> assertions are dropped:
/// e2e dialogs have no default value.
/// </summary>
public sealed class PageDialogTests : PlaywrightTest
{
    [Test]
    public async Task Should_fire()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.OnDialogAsync(async dialog =>
        {
            Assert.That(dialog.Type, Is.EqualTo(DialogType.Alert));
            Assert.That(dialog.Message, Is.EqualTo("yo"));
            await dialog.AcceptAsync();
        });
        await Browser.EvaluateAsync<object>("() => alert('yo')");
    }

    [Test]
    public async Task Should_allow_accepting_prompts()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.OnDialogAsync(async dialog =>
        {
            Assert.That(dialog.Type, Is.EqualTo(DialogType.Prompt));
            Assert.That(dialog.Message, Is.EqualTo("question?"));
            await dialog.AcceptAsync("answer!");
        });
        var result = await Browser.EvaluateAsync<string>("() => prompt('question?', 'yes.')");
        Assert.That(result, Is.EqualTo("answer!"));
    }

    [Test]
    public async Task Should_dismiss_the_prompt()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.OnDialogAsync(dialog => dialog.DismissAsync());
        var result = await Browser.EvaluateAsync<string>("() => prompt('question?')");
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Should_accept_the_confirm_prompt()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.OnDialogAsync(async dialog =>
        {
            await dialog.AcceptAsync();
        });
        var result = await Browser.EvaluateAsync<bool>("() => confirm('boolean?')");
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task Should_dismiss_the_confirm_prompt()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.OnDialogAsync(async dialog =>
        {
            await dialog.DismissAsync();
        });
        var result = await Browser.EvaluateAsync<bool>("() => confirm('boolean?')");
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Should_handle_multiple_alerts()
    {
        await Browser.OnDialogAsync(async dialog =>
        {
            try
            {
                await dialog.AcceptAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Upstream: `.catch(() => {})`.
            }
        });
        await Browser.GotoAsync(Content("""
            <p>Hello World</p>
            <script>
              alert('Please dismiss this dialog');
              alert('Please dismiss this dialog');
              alert('Please dismiss this dialog');
            </script>
            """));
        Assert.That(await Browser.Locator("p").TextContentAsync(), Is.EqualTo("Hello World"));
    }

    [Test]
    public async Task Should_handle_multiple_confirms()
    {
        await Browser.OnDialogAsync(async dialog =>
        {
            try
            {
                await dialog.AcceptAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Upstream: `.catch(() => {})`.
            }
        });
        await Browser.GotoAsync(Content("""
            <p>Hello World</p>
            <script>
              confirm('Please confirm me?');
              confirm('Please confirm me?');
              confirm('Please confirm me?');
            </script>
            """));
        Assert.That(await Browser.Locator("p").TextContentAsync(), Is.EqualTo("Hello World"));
    }

    [Test]
    public async Task Should_auto_dismiss_the_prompt_without_listeners()
    {
        await Browser.GotoAsync("/empty.html");
        var result = await Browser.EvaluateAsync<string>("() => prompt('question?')");
        Assert.That(result, Is.Null);
        var error = await Failure(() => Browser.TitleAsync());
        Assert.That(error?.Message, Does.Contain("unhandled prompt dialog: question?"));
    }

    [Test]
    public async Task Should_auto_dismiss_the_alert_without_listeners()
    {
        await Browser.GotoAsync(Content("""<div onclick="window.alert(123); window._clicked=true">Click me</div>"""));
        var error = await Failure(async () =>
        {
            await Browser.Locator("div").TapAsync();
            await Browser.TitleAsync();
        });
        Assert.That(error?.Message, Does.Contain("unhandled alert dialog: 123"));
        Assert.That(await Browser.EvaluateAsync<bool>("window._clicked"), Is.True);
    }

    /// <summary>What <paramref name="act"/> threw, or null when it did not throw (upstream's <c>failure</c>).</summary>
    private static async Task<Exception?> Failure(Func<Task> act)
    {
        try
        {
            await act();
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ex;
        }
    }
}
