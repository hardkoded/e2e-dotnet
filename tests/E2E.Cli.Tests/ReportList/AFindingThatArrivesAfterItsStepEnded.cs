// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

public sealed partial class ExploreRunsTests
{
    /// <summary>Upstream: packages/e2e/tests/unit/report-list.test.ts, describe('a finding that arrives after its step ended'), nested in 'explore runs' (tester-army/e2e#2270).</summary>
    public sealed class AFindingThatArrivesAfterItsStepEnded
    {
        private const string LateLine = "⚑ high issue  Cart total shows $0.00 with two items (/cart) · step 1";

        private static readonly ReportExploreFinding Late = Finding with { ReportedAt = Start.AddSeconds(34) };

        private static void UntilStepOneEnded(global::E2E.Cli.Report.ListReporter reporter)
        {
            reporter.Handle(RunStarted(targets: ["web"]));
            reporter.Handle(PlanOn("web"));
            reporter.Handle(TestStarted("t1", Goal, "web", "explore"));
            reporter.Handle(Explore(new ExploreProgress.Started(Goal, Budgets)));
            reporter.Handle(Explore(new ExploreProgress.StepStarted(Step1)));
            reporter.Handle(
                Explore(
                    new ExploreProgress.StepFinished(
                        Step1Done with
                        {
                            Status = ReportExploreStepStatus.Exhausted,
                            Summary = null,
                        }
                    )
                )
            );
        }

        [Fact]
        public void Prints_it_in_the_log_under_the_row_already_printed_naming_its_step()
        {
            var output = new Capture();
            var reporter = PlainReporter(output);
            UntilStepOneEnded(reporter);
            reporter.Handle(Explore(new ExploreProgress.Planning(false)));
            reporter.Handle(Explore(new ExploreProgress.FindingReported(Late)));
            Assert.Equal(["   × 1  Cart 32.00s ended at its limit", "   " + LateLine], output.Lines.TakeLast(2));
        }

        [Fact]
        public void Shows_it_in_the_live_window_at_once_naming_its_step_while_the_planner_works_and_through_the_next_step()
        {
            var output = new LiveCapture();
            var reporter = PlainReporter(output, live: true);
            string Frame() => StripAnsi(output.Chunks[^1]);
            UntilStepOneEnded(reporter);
            reporter.Handle(Explore(new ExploreProgress.Planning(false)));
            reporter.Handle(Explore(new ExploreProgress.FindingReported(Late)));
            Assert.Contains(LateLine, Frame(), StringComparison.Ordinal);
            Assert.Contains("× 1  Cart 32.00s · 1 finding ended at its limit", Frame(), StringComparison.Ordinal);
            reporter.Handle(Explore(new ExploreProgress.StepStarted(Step2)));
            Assert.Contains(LateLine, Frame(), StringComparison.Ordinal);
            Assert.Contains("Steps  1 done of 4 · step 2 running", Frame(), StringComparison.Ordinal);
            reporter.Handle(
                Explore(
                    new ExploreProgress.StepFinished(
                        Step2Done with
                        {
                            Status = ReportExploreStepStatus.Passed,
                            Duration = TimeSpan.FromMilliseconds(9_000),
                        }
                    )
                )
            );
            Assert.DoesNotContain(LateLine, Frame(), StringComparison.Ordinal);
        }
    }
}
