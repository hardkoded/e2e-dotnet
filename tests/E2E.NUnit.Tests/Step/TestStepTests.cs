// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.Step;

/// <summary>
/// <c>test.describe('test.step')</c> of upstream's <c>apps/testbed/tests/step.e2e.ts</c> (tester-army/e2e#2312), against the playground's
/// <c>/todos</c> page. All two tests are ported. The class carries the <c>step</c> category (upstream's <c>tags: ['step']</c>).
/// </summary>
[Category("step")]
public sealed class TestStepTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null
    );

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    // Upstream's beforeEach. NUnit runs the base class's SetUp (which starts the session) before this one.
    [SetUp]
    public Task OpenTheTodosPage() => Step("Open the todos page", async () => await App.OpenAsync("/todos"));

    [Test]
    public async Task Names_a_group_of_calls_and_returns_what_its_body_returns()
    {
        var count = await Step("Add two todos", async () =>
        {
            foreach (var title in new[] { "Write spec", "Ship runner" })
            {
                await Step($"Add \"{title}\"", async () =>
                {
                    await Screen.GetByLabel("New todo").FillAsync(title);
                    await Screen.GetByLabel("New todo").PressAsync("Enter");
                });
            }

            return await Screen.GetByTestId("todo").CountAsync();
        });
        Assert.That(count, Is.EqualTo(2));

        await Step("Check the counter", async () =>
            await Expect.That(Screen.GetByRole("status", "Remaining")).ToHaveTextAsync("2 remaining")
        );
    }

    [Test]
    public async Task Passes_the_error_of_a_failed_call_on_to_the_test()
    {
        var failure = await Assert.CatchAsync(async () =>
            await Step("Tap a button that is not there", async () =>
                await Screen
                    .GetByRole("button", "Missing")
                    .TapAsync(new TapOptions { Timeout = TimeSpan.FromMilliseconds(200) })
            )
        );
        Assert.That(failure, Is.InstanceOf<Exception>());
        await Expect.That(Screen.GetByTestId("todo")).ToHaveCountAsync(0);
    }
}
