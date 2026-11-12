// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.NUnit;
using E2E.Playground;

namespace E2E.Agent.Tests.ScrollTo;

/// <summary>
/// <c>scroll_to</c> by text over the playground's windowed <c>/scroll</c> page, driven by a scripted executor, so it needs no model.
/// Ported from upstream's <c>apps/testbed/tests-scroll/scroll-to.e2e.ts</c>, which runs under its own config
/// (<c>e2e.scroll.config.ts</c>) because that config makes the default agent the scripted executor; here the test class does,
/// through <see cref="AgentExecutor"/>. It stays in this project for the playground and runs as a real-model test only
/// because the project's tests do: nothing here calls a model.
/// </summary>
[Category("RealModel")]
public sealed partial class ScrollToATextOnAWindowedListTests : E2ETest
{
    protected override string? BaseUrl => Testbed.Url;

    protected override IStepExecutor? AgentExecutor => new ScrollToRow();

    [TestCase("Row 12", TestName = "Reaches_a_near_row_and_taps_it")]
    [TestCase("Row 60", TestName = "Reaches_a_middle_row_and_taps_it")]
    [TestCase("Row 118", TestName = "Reaches_a_deep_row_and_taps_it")]
    public async Task Reaches_a_row_and_taps_it(string row)
    {
        await Browser.GotoAsync("/scroll");
        await Agent.ActAsync($"scroll to {row} and tap it");
        await Expect.That(Screen.GetByRole("status", "Picked row")).ToHaveTextAsync($"picked {row}");
    }

    [GeneratedRegex(@"Row \d+", RegexOptions.CultureInvariant)]
    private static partial Regex RowName();

    [GeneratedRegex(@"#(\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex Reference();

    /// <summary>Upstream's <c>scrollToRow</c>: the instruction names the row, the executor pages to it and taps it. A replay would skip the paging it exists to run, so its cache is off.</summary>
    private sealed class ScrollToRow : IStepExecutor
    {
        public string Name => "scroll-to-row";

        public string? Version => "1";

        public ExecutorCache Cache => ExecutorCache.Off;

        public async Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken cancellationToken)
        {
            var match = RowName().Match(context.Step.Instruction);
            if (!match.Success)
            {
                return new StepVerdict(StepVerdictStatus.Failed, "the step names no row", "STEP_NO_CONCLUSION");
            }

            var row = match.Value;
            await context.ObserveAsync(null, cancellationToken);
            await context.Actions.ScrollUntilAsync(row, ScrollDirection.Down, null, cancellationToken);
            var screen = await context.ObserveAsync(null, cancellationToken);
            var line = screen.Text.Split('\n').FirstOrDefault(entry => entry.Contains($"button \"{row}\"", StringComparison.Ordinal));
            var id = line is null ? null : Reference().Match(line) is { Success: true } reference ? reference.Groups[1].Value : null;
            if (id is null)
            {
                return new StepVerdict(StepVerdictStatus.Failed, $"{row} is not on the screen", "STEP_NO_CONCLUSION");
            }

            await context.Actions.TapAsync(new ExecutorTarget(id), cancellationToken);
            return new StepVerdict(StepVerdictStatus.Passed, $"tapped {row}");
        }
    }
}
