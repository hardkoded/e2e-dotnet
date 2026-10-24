// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WinAppBenchmark.E2ETests.Fixtures;

namespace WinAppBenchmark.E2ETests.Scenarios;

/// <summary>
/// <c>Agent.ActAsync</c> against every scenario, one step each: the step gets the catalog description as its
/// goal and reads the rest off the screen; the deterministic check on the scenario's success message decides
/// the test.
/// </summary>
public sealed class ScenariosTests : BenchmarkTest
{
    /// <param name="Name">The home-list row, which is also the scenario's screen title.</param>
    /// <param name="Goal">What the step is asked to do.</param>
    /// <param name="Success">What <c>success-message</c> shows at the end.</param>
    /// <param name="Pixels">The screen's tree is hidden on purpose, so the success message never enters it: the goal tells the model the screen lists nothing and success is judged from a screenshot.</param>
    /// <param name="WithAccount">The goal refers to the benchmark account, which comes in as params.</param>
    /// <param name="Gap">Why the harness cannot finish this scenario yet. The test is declared and skipped with the reason, and turns on the day the capability lands.</param>
    public sealed record Scenario(string Name, string Goal, string Success, bool Pixels = false, bool WithAccount = false, string? Gap = null)
    {
        public override string ToString() => Name;
    }

    private const string PixelsGap = "agent.act gives the model the tree only, never a screenshot, and this screen lists nothing in the tree";

    public static readonly IReadOnlyList<Scenario> All =
    [
        // The hint on screen shows the declared credential, which reaches the model redacted, so the account comes in as params instead.
        new("Login Form", "log in with the given username and password", "Logged in successfully", WithAccount: true),
        // Item 137 is the fifth page; the configured action budget (agents.default.maxSteps) covers the scrolls.
        new("Infinite Scroll List", "scroll the paginated list until Item 137 appears, then tap it", "Found item 137"),
        new("Modal Flow", "open the modal, continue, and confirm the dialog that follows", "Flow completed"),
        new("Bottom Tabs", "go to the Actions tab and complete the action there", "Action completed"),
        new("Text Input Variations", "fill the username, the 4-digit PIN, and at least ten characters of notes, then submit", "Form submitted"),
        new("Broken Accessibility", "complete the flow the screen describes until it says Access granted", "Access granted", Pixels: true, Gap: PixelsGap),
        new("Vision Only", "complete the flow the screen describes until it says Sequence complete", "Sequence complete", Pixels: true, Gap: PixelsGap),
        new("Gestures", "long-press the first target, swipe the card left, then double-tap the last target", "All gestures completed", Gap: "agent.act has no long press, and the Windows tool pack has no swipe, so the model cannot finish the first two steps"),
        new("Async States", "wait for the screen to load, press Refresh to reveal the reward, then claim the reward", "Reward claimed"),
        new("Debounced Search", "search for Benchmark Target and select exactly that result once it loads", "Selected Benchmark Target"),
        new("Huge Virtualized List", "scroll to Row 0512 and tap it; the list has 600 rows and off-screen rows are not in the tree", "Found Row 0512"),
        // The values are spelled out so the recording replays.
        new("Flattened Registration Form", "fill the registration form with first name Ada, last name Lovelace, email ada@example.com, and password Lovelace1815, then sign up until it says Account created", "Account created", Pixels: true, Gap: PixelsGap),
        // The account is read off the screenshot, so the typed values are a gap in the recording by design.
        new("Flattened Login", "log in with the email and password the screen shows as a hint", "Logged in successfully", Pixels: true, Gap: PixelsGap),
        new("Sticky Chrome Target", "scroll down to the Accept terms button under the sticky footer, accept the terms, then tap Continue in the footer", "Terms accepted"),
        new("Product Catalog", "open Trail Mix 500 g (not a look-alike), set the quantity to 2, add it to the cart, then open the cart", "Order ready: 2 × Trail Mix 500 g"),
        new("Error Recovery", "retry the failed load, then delete the draft and confirm", "Draft deleted"),
        new("Choice Controls", "order a Medium pizza with exactly Cheese and Olives, enable rush delivery, and place the order", "Order placed: Medium with Cheese, Olives (rush)"),
        new("Sequential Onboarding", "complete the signup wizard with email jane@example.com, first name Jane, last name Merchant, and phone 0612435678", "Onboarding complete"),
    ];

    public static IEnumerable<TestCaseData> Cases() =>
        All.Select(scenario => new TestCaseData(scenario).SetName($"Act_completes_{scenario.Name.Replace(' ', '_')}").SetCategory(scenario.Name));

    [TestCaseSource(nameof(Cases))]
    public async Task Act_completes(Scenario scenario)
    {
        if (scenario.Gap is not null)
        {
            Assert.Ignore(scenario.Gap);
        }

        await OpenScenarioAsync(scenario.Name);
        var instruction = scenario.Pixels
            ? $"Complete this scenario as the screen instructs: {scenario.Goal}. The screen lists nothing useful; take a screenshot and work from it."
            : $"Complete this scenario as the screen instructs: {scenario.Goal}";
        var account = scenario.WithAccount ? Credentials.User("benchmark") : null;
        var options = new ActOptions
        {
                        Params = account is null ? null : new Dictionary<string, object?> { ["username"] = account.Username, ["password"] = account.Password },
        };

        await Agent.ActAsync(instruction, options);
        if (scenario.Pixels)
        {
            // The tree is hidden on purpose, so the message is judged from pixels.
            await Agent.AssertAsync($"the screen shows the text \"{scenario.Success}\"", new AssertOptions { Vision = VisionMode.Only });
            return;
        }

        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync(scenario.Success);
    }
}
