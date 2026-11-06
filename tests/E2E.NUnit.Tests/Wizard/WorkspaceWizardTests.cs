// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.Wizard;

/// <summary>
/// <c>test.describe('workspace wizard', { serial: true })</c> of upstream's <c>apps/testbed/tests/wizard.e2e.ts</c>, against the playground's
/// <c>/wizard</c> page. All three members are ported, each <c>Partial_</c>: upstream runs them as a serial group that keeps one session
/// (and so the page's state) from member to member, and the port has no serial groups (COMPATIBILITY.md), so a session starts for each test.
/// Each test therefore redoes the earlier steps of the group through the same locators, then runs its own steps and assertions as upstream.
/// The <c>wizard</c> category is upstream's <c>tags: ['wizard']</c>. Tracked by hardkoded/e2e-dotnet#1241.
/// </summary>
[Category("wizard")]
public sealed class WorkspaceWizardTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    private async Task Step1Async()
    {
        await App.OpenAsync("/wizard");
        await Screen.GetByLabel("Workspace name").FillAsync("Rocketry");
        await Screen.GetByRole("button", "Next").TapAsync();
        await Expect.That(Screen.GetByRole("heading", "Step 2: Plan")).ToBeVisibleAsync();
    }

    private async Task<string> Step2Async()
    {
        await Step1Async();
        await Screen.GetByLabel("Plan").SelectOptionAsync("Pro");
        var chosenPlan = await Screen.GetByLabel("Plan").InputValueAsync();
        await Screen.GetByRole("button", "Next").TapAsync();
        await Expect.That(Screen.GetByRole("heading", "Step 3: Confirm")).ToBeVisibleAsync();
        return chosenPlan;
    }

    [Test]
    public async Task Partial_Step_1_name_the_workspace()
    {
        await Step1Async();
    }

    [Test]
    public async Task Partial_Step_2_pick_a_plan()
    {
        await Step2Async();
    }

    [Test]
    public async Task Partial_Step_3_confirm_with_state_from_earlier_members()
    {
        var chosenPlan = await Step2Async();

        await Screen.GetByRole("button", "Create workspace").TapAsync();
        await Expect.That(Screen.GetByRole("status", "Summary")).ToHaveTextAsync($"Created \"Rocketry\" on the {chosenPlan} plan");
    }
}
