// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace WebBenchmark.E2ETests.TestsAgent.LoginForm;

/// <summary>
/// Ported from upstream's <c>apps/web-benchmark/tests-agent/login-form.e2e.ts</c> (no <c>describe</c>). The password is a secret:
/// the model sees only its name and purpose, and the fill runs through the authorized secret tool. The deterministic check after the
/// step decides the test; the model only drives.
/// </summary>
public sealed class LoginFormTests : TestsAgentTest
{
    [Test]
    public async Task Act_signs_in_with_the_benchmark_account()
    {
        var account = Credentials.User("benchmark");
        await App.OpenAsync("/e/login-form");
        await Agent.ActAsync("log in with the given credentials", new ActOptions
        {
            Params = new Dictionary<string, object?> { ["email"] = account.Username, ["password"] = account.Password },
        });
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Logged in successfully");
    }
}
