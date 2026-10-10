// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit;
using E2E.Playground;

namespace E2E.Agent.Tests.Act;

/// <summary>
/// <c>Agent.ActAsync</c> against a real model: multi-action flows planned and executed by the default
/// executor, verified deterministically after. Ported from upstream's <c>apps/testbed/tests-agent/act.e2e.ts</c>.
/// "act taps a pin painted on a canvas through the pixel tier" is not ported: the port has no vision tier.
/// </summary>
[Category("RealModel")]
public sealed class ActTests : E2ETest
{
    protected override string? BaseUrl => Testbed.Url;

    [OneTimeSetUp]
    public void ConfigureAdminCredential()
    {
        Environment.SetEnvironmentVariable("E2E_USER_ADMIN_USERNAME", "admin");
        Environment.SetEnvironmentVariable("E2E_USER_ADMIN_PASSWORD", "admin-pass");
    }

    [Test]
    public async Task Act_drives_a_multi_action_todo_flow()
    {
        await Browser.GotoAsync("/todos");
        await Agent.ActAsync("add two todos named \"Buy milk\" and \"Walk the dog\", then mark \"Buy milk\" as done");
        await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("1 remaining");
        await Expect.That(Screen.GetByText("Buy milk")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByText("Walk the dog")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Act_completes_the_workspace_wizard_end_to_end()
    {
        await Browser.GotoAsync("/wizard");
        await Agent.ActAsync("create a workspace named \"Atlas\" on the Pro plan by walking through the wizard");
        await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("Created \"Atlas\" on the Pro plan");
    }

    [Test]
    public async Task Act_signs_in_with_a_secret_credential()
    {
        await Browser.GotoAsync("/login");
        var admin = Credentials.User("admin");

        // The password is a Secret: the model sees only its name and purpose, and the fill runs through
        // the authorized type_secret tool.
        await Agent.ActAsync("sign in with the given credentials", new ActOptions
        {
            Params = new Dictionary<string, object?> { ["username"] = admin.Username, ["password"] = admin.Password },
        });
        await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("Welcome back, admin!");
    }
}
