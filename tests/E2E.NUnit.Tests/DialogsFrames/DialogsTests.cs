// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.NUnit.Tests.DialogsFrames;

/// <summary>
/// <c>test.describe('dialogs')</c> of upstream's <c>apps/testbed/tests/dialogs-frames.e2e.ts</c>, against the playground's
/// <c>/dialogs</c> page.
/// </summary>
public sealed class DialogsTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Accepting_a_confirm_dialog()
    {
        await App.OpenAsync("/dialogs");
        var dispose = await Browser.OnDialogAsync(DialogHandler.Accept);
        await Screen.GetByRole("button", "Delete everything").TapAsync();
        await Expect.That(Screen.GetByRole("status", "Decision")).ToHaveTextAsync("deleted");
        await dispose();
    }

    [Test]
    public async Task Dismissing_a_confirm_dialog()
    {
        await App.OpenAsync("/dialogs");
        var dispose = await Browser.OnDialogAsync(DialogHandler.Dismiss);
        await Screen.GetByRole("button", "Delete everything").TapAsync();
        await Expect.That(Screen.GetByRole("status", "Decision")).ToHaveTextAsync("kept");
        await dispose();
    }

    [Test]
    public async Task A_throwing_handler_dismisses_its_unanswered_dialog()
    {
        await App.OpenAsync("/dialogs");
        await Browser.OnDialogAsync(_ => throw new InvalidOperationException("handler exploded"));
        Assert.That(await Browser.EvaluateAsync<bool>("confirm(\"Really delete everything?\")"), Is.False);
        var error = await Assert.ThrowsAsync<EngineException>(() => Browser.TitleAsync());
        Assert.That(error!.Code, Is.EqualTo("ENGINE_FAILURE"));
        Assert.That(await Browser.TitleAsync(), Is.EqualTo("Dialogs"));
    }

    [Test]
    public async Task Custom_dialog_handlers_read_the_message()
    {
        await App.OpenAsync("/dialogs");
        var message = "";
        var type = DialogType.Alert;
        var dispose = await Browser.OnDialogAsync(async dialog =>
        {
            message = dialog.Message;
            type = dialog.Type;
            await dialog.AcceptAsync();
        });
        await Screen.GetByRole("button", "Delete everything").TapAsync();
        await Expect.That(Screen.GetByRole("status", "Decision")).ToHaveTextAsync("deleted");
        Assert.That(message, Is.EqualTo("Really delete everything?"));
        Assert.That(type, Is.EqualTo(DialogType.Confirm));
        await dispose();
    }
}
