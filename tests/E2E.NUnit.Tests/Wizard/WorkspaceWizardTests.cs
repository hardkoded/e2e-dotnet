// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.Wizard;

/// <summary>
/// <c>test.describe('workspace wizard', { serial: true })</c> of upstream's <c>apps/testbed/tests/wizard.e2e.ts</c>, against the playground's
/// <c>/wizard</c> page. The three members share one session through <c>[Serial]</c>, so the page's state carries from member to member, and
/// <c>chosenPlan</c> is a field they share, as upstream's variable. The <c>wizard</c> category is upstream's <c>tags: ['wizard']</c>.
/// </summary>
[Category("wizard")]
[Serial]
public sealed class WorkspaceWizardTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    private string _chosenPlan = "";

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Step_1_name_the_workspace()
    {
        await App.OpenAsync("/wizard");
        await Screen.GetByLabel("Workspace name").FillAsync("Rocketry");
        await Screen.GetByRole("button", "Next").TapAsync();
        await Expect.That(Screen.GetByRole("heading", "Step 2: Plan")).ToBeVisibleAsync();
    }

    [Test, DependsOnTest(nameof(Step_1_name_the_workspace))]
    public async Task Step_2_pick_a_plan()
    {
        await Screen.GetByLabel("Plan").SelectOptionAsync("Pro");
        _chosenPlan = "Pro";
        await Screen.GetByRole("button", "Next").TapAsync();
        await Expect.That(Screen.GetByRole("heading", "Step 3: Confirm")).ToBeVisibleAsync();
    }

    [Test, DependsOnTest(nameof(Step_2_pick_a_plan))]
    public async Task Step_3_confirm_with_state_from_earlier_members()
    {
        await Screen.GetByRole("button", "Create workspace").TapAsync();
        await Expect.That(Screen.GetByRole("status", "Summary")).ToHaveTextAsync($"Created \"Rocketry\" on the {_chosenPlan} plan");
    }
}
