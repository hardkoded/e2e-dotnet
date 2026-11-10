// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.PlaybookCleanup;

/// <summary>Ported from <c>describe('playbook cleanup')</c> in upstream's <c>apps/web-benchmark/tests/playbook-cleanup.e2e.ts</c>.</summary>
public sealed class PlaybookCleanupTests : BenchmarkTest
{
    private const string Target = "Launch Approval Draft";

    [SetUp]
    public async Task OpenThePlaybookCleanup()
    {
        await App.OpenAsync("/e/playbook-cleanup");
        await Expect.That(Screen.GetByRole("heading", new RoleOptions { Level = 3 })).ToHaveTextAsync(["NDA Review", Target, "Vendor Onboarding"]);
    }

    [Test]
    public async Task Deleting_the_draft_removes_it_from_an_exact_name_search()
    {
        var more = Screen.GetByRole("button", $"More actions for {Target}");
        await more.TapAsync();
        await Expect.That(more).ToBeExpandedAsync();
        await Screen.GetByRole("menu", $"Actions for {Target}").GetByRole("menuitem", "Delete").TapAsync();

        var dialog = Screen.GetByRole("dialog", "Remove Playbook");
        await Expect.That(dialog).ToContainTextAsync($"Are you sure you want to remove \"{Target}\"?");
        await dialog.GetByRole("button", "Remove").TapAsync();
        await Expect.That(dialog).ToBeHiddenAsync();
        await Expect.That(Screen.GetByRole("heading", new RoleOptions { Level = 3 })).ToHaveTextAsync(["NDA Review", "Vendor Onboarding"]);

        await Screen.GetByLabel("Search playbooks").FillAsync(Target);
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync($"No playbooks match \"{Target}\".");
    }

    [Test]
    public async Task Cancelling_the_dialog_keeps_the_draft_searchable()
    {
        await Screen.GetByRole("button", $"More actions for {Target}").TapAsync();
        await Screen.GetByRole("menuitem", "Delete").TapAsync();
        var dialog = Screen.GetByRole("dialog", "Remove Playbook");
        await dialog.GetByRole("button", "Cancel").TapAsync();
        await Expect.That(dialog).ToBeHiddenAsync();

        await Screen.GetByLabel("Search playbooks").FillAsync(Target);
        await Expect.That(Screen.GetByRole("heading", new RoleOptions { Level = 3 })).ToHaveTextAsync([Target]);
        await Expect.That(Screen.GetByTestId("success-message")).ToBeHiddenAsync();
    }
}
