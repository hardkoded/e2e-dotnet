// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.GuestSteps;

/// <summary>Upstream: packages/e2e/tests/unit/guest-steps.test.ts, describe('the steps of a session a test drives beside its own').</summary>
public sealed class TheStepsOfASessionATestDrivesBesideItsOwnTests
{
    private static readonly string Root = Path.GetFullPath("/projects/shop");

    // A test's recorder, and a second session's recorder whose steps are mirrored into it as the actor.
    private static (
        StepRecorder Test,
        StepRecorder Session,
        GuestMirror Mirror,
        List<global::E2E.StepProgress> Told
    ) Pair(string actor = "launch-1", string? sessionRoot = null, Func<string, string>? redact = null)
    {
        var told = new List<global::E2E.StepProgress>();
        var test = new StepRecorder(
            "test-attempt",
            new StepRecorderOptions
            {
                ProjectRoot = Root,
                OnProgress = progress =>
                {
                    lock (told)
                    {
                        told.Add(progress);
                    }
                },
                Redact = redact,
            }
        );
        var mirror = E2E.GuestSteps.MirrorGuestSteps(test, actor, sessionRoot ?? Root);
        var session = new StepRecorder("session-attempt", new StepRecorderOptions { Mirror = mirror.Progress });
        return (test, session, mirror, told);
    }

    private static StepEvent Event(StepEventKind kind, string detail, string? reasoning = null) =>
        new()
        {
            Kind = kind,
            StartedAt = DateTimeOffset.UtcNow,
            DurationMs = 3,
            Detail = detail,
            Reasoning = reasoning,
        };

    [Fact]
    public async Task Records_each_under_the_sessions_name_at_the_index_its_start_takes()
    {
        var (test, session, _, _) = Pair();
        await test.RunAsync(StepKind.App, "app.open", "/", () => Task.CompletedTask);
        await session.RunAsync(StepKind.Locator, "locator.tap", "Confirm", () =>
        {
            session.RecordEvent(Event(StepEventKind.Engine, "tap button \"Confirm\""));
            return Task.CompletedTask;
        });
        await test.RunAsync(StepKind.Locator, "locator.tap", "Send", () => Task.CompletedTask);

        Assert.Equal(
            [
                (0, "test-attempt:0", (string?)null, "app.open", "/"),
                (1, "test-attempt:1", "launch-1", "locator.tap", "Confirm"),
                (2, "test-attempt:2", null, "locator.tap", "Send"),
            ],
            test.All().Select(step => (step.Index, step.Id, step.Actor, step.Api, step.Label))
        );
        Assert.Equal(["tap button \"Confirm\""], test.All()[1].Events.Select(stepEvent => stepEvent.Detail));
        Assert.Empty(test.All()[1].Artifacts);
    }

    [Fact]
    public async Task Tells_reporters_of_the_step_under_the_tests_own_identity_with_the_sessions_name()
    {
        var (_, session, _, told) = Pair("launch-2");
        await session.RunAsync(StepKind.App, "app.open", "/b", () => Task.CompletedTask);

        Assert.Equal(
            [
                (StepProgressPhase.Start, new StepProgressIdentity("test-attempt", 0, "test-attempt:0", 0, "launch-2")),
                (StepProgressPhase.End, new StepProgressIdentity("test-attempt", 0, "test-attempt:0", 0, "launch-2")),
            ],
            told.Select(progress => (progress.Phase, progress.Identity!))
        );
    }

    [Fact]
    public async Task Keeps_a_failed_steps_status_and_error()
    {
        var (test, session, _, _) = Pair();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.RunAsync(StepKind.Locator, "locator.tap", "Missing", () =>
                throw new InvalidOperationException("no such button")
            )
        );

        var step = test.All()[0];
        Assert.Equal(("launch-1", StepStatus.Failed, "no such button"), (step.Actor, step.Status, step.Error?.Message));
    }

    [Fact]
    public async Task Takes_what_an_agent_step_spent_as_a_copy_the_session_cannot_change_afterwards()
    {
        var (test, session, _, _) = Pair();
        List<string> calls = ["tap({\"target\":\"n1\"})"];
        await session.RunAsync(StepKind.Agent, "agent.act", "Confirm the order", () =>
        {
            session.AddModelCall(
                new ExecutorModelCall
                {
                    Provider = "gateway",
                    ModelId = "m",
                    InputTokens = 10,
                    OutputTokens = 2,
                }
            );
            session.AddModelCall(
                new ExecutorModelCall
                {
                    Provider = "gateway",
                    ModelId = "m",
                    InputTokens = 20,
                    OutputTokens = 3,
                }
            );
            session.AttachAgentDetails(
                new StepAgentDetails
                {
                    Metrics = new StepMetrics
                    {
                        ModelCalls = 2,
                        ActionSteps = 1,
                        ObservationBytes = 9,
                        ContextBytes = 9,
                        LedgerBytes = 0,
                    },
                    Turns = [new StepTurn(1, calls, "tapped")],
                }
            );
            return Task.CompletedTask;
        });

        var step = test.All()[0];
        Assert.Equal(
            (StepKind.Agent, "launch-1", 2, 30L),
            (step.Kind, step.Actor, step.Metrics!.ModelCalls, step.Model!.InputTokens)
        );
        Assert.Equal(["tap({\"target\":\"n1\"})"], step.Turns![0].Calls);
        calls[0] = "changed by the session later";
        Assert.Equal(["tap({\"target\":\"n1\"})"], test.All()[0].Turns![0].Calls);
    }

    [Fact]
    public async Task Redacts_the_prose_the_session_reports_with_the_tests_own_redactor_whatever_the_session_knew()
    {
        const string value = "known-to-the-test-only-7719";
        var (test, session, _, _) = Pair(redact: text => text.Replace(value, "<secret:otp>", StringComparison.Ordinal));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.RunAsync(StepKind.Agent, "agent.act", $"the code {value}", () =>
            {
                session.RecordEvent(Event(StepEventKind.Model, $"typed {value}", $"I read {value}"));
                session.AttachAgentDetails(
                    new StepAgentDetails
                    {
                        Explanation = $"the screen showed {value}",
                        Turns = [new StepTurn(1, [$"type({{\"text\":\"{value}\"}})"], $"typed {value}")],
                        Cache = new CacheInfo
                        {
                            Mode = "missed",
                            ReplayedActions = 0,
                            TotalActions = 1,
                            Detail = $"the screen read {value}",
                        },
                    }
                );
                throw new InvalidOperationException($"the screen read {value}");
            })
        );

        var step = test.All()[0];
        var everything = string.Join(
            '\n',
            new[] { step.Label, step.Explanation, step.Error?.Message, step.Cache?.Detail }
                .Concat(step.Turns!.SelectMany(turn => turn.Calls.Append(turn.Outcome)))
                .Concat(step.Events.SelectMany(stepEvent => new[] { stepEvent.Detail, stepEvent.Reasoning }))
        );
        Assert.DoesNotContain(value, everything, StringComparison.Ordinal);
        Assert.Equal("the code <secret:otp>", step.Label);
        Assert.Equal("the screen showed <secret:otp>", step.Explanation);
        Assert.Equal("the screen read <secret:otp>", step.Error!.Message);
        Assert.Equal(["type({\"text\":\"<secret:otp>\"})"], step.Turns![0].Calls);
        Assert.Equal("typed <secret:otp>", step.Turns[0].Outcome);
        Assert.Equal("the screen read <secret:otp>", step.Cache!.Detail);
        Assert.Equal(("typed <secret:otp>", "I read <secret:otp>"), (step.Events[0].Detail, step.Events[0].Reasoning));
    }

    [Fact]
    public void Reads_the_sessions_test_lines_against_the_tests_project_when_the_sessions_root_is_another_directory()
    {
        var (test, _, mirror, _) = Pair(sessionRoot: Path.Combine(Root, "packages", "bot"));
        StepRecord Theirs(string id, string file) =>
            new()
            {
                Id = id,
                Index = 0,
                Kind = StepKind.App,
                Api = "app.open",
                Label = "/",
                Source = new SourceLocation(file, 12, 3),
                Status = StepStatus.Failed,
                StartedAt = DateTimeOffset.UtcNow,
                DurationMs = 4,
                Error = new SerializedError(
                    "test",
                    "LOCATOR_NOT_FOUND",
                    "nothing matched",
                    false,
                    new SourceLocation(file, 12, 9)
                ),
            };

        foreach (
            var record in new[]
            {
                Theirs("inside", "../../tests/shop.e2e.ts"),
                Theirs("outside", "../../../elsewhere/run.ts"),
            }
        )
        {
            mirror.Progress(
                new global::E2E.StepProgress
                {
                    Phase = StepProgressPhase.Start,
                    Kind = StepKind.App,
                    Api = "app.open",
                    Label = "/",
                },
                record
            );
            mirror.Progress(
                new global::E2E.StepProgress
                {
                    Phase = StepProgressPhase.End,
                    Kind = StepKind.App,
                    Api = "app.open",
                    Label = "/",
                    Status = StepStatus.Failed,
                    DurationMs = 4,
                    ModelCalls = 0,
                },
                record
            );
        }

        Assert.Equal(
            [
                (new SourceLocation("tests/shop.e2e.ts", 12, 3), new SourceLocation("tests/shop.e2e.ts", 12, 9)),
                ((SourceLocation?)null, (SourceLocation?)null),
            ],
            test.All().Select(step => (step.Source, step.Error?.Source))
        );
    }

    [Fact]
    public async Task Leaves_the_sessions_steps_out_of_what_an_agent_of_the_test_reads_as_the_steps_before_its_own_and_files_no_event_of_the_test_under_them()
    {
        var (test, session, _, _) = Pair();
        await test.RunAsync(StepKind.App, "app.open", "/", () => Task.CompletedTask);
        await session.RunAsync(StepKind.App, "app.open", "/other", () => Task.CompletedTask);

        Assert.Equal(["/"], test.Completed().Select(step => step.Label));

        // The app navigated on its own after the session's step: that belongs to the test's last own step.
        test.RecordAppEvent(new AppEvent.Navigation("navigated to /done"));
        Assert.Equal([1, 0], test.All().Select(step => step.Events.Count));
    }

    [Fact]
    public async Task Tells_a_step_the_attempt_ended_under_as_cancelled_with_what_it_did_so_far_and_drops_what_the_session_reports_afterwards()
    {
        var (test, session, mirror, told) = Pair();
        var finish = new TaskCompletionSource();
        var running = session.RunAsync(StepKind.Locator, "locator.waitFor", "Slow", () =>
        {
            session.RecordEvent(Event(StepEventKind.Engine, "looked once"));
            return finish.Task;
        });
        mirror.Cut();

        Assert.Equal(("launch-1", StepStatus.Cancelled), (test.All()[0].Actor, test.All()[0].Status));
        Assert.Equal(["looked once"], test.All()[0].Events.Select(stepEvent => stepEvent.Detail));
        var last = told[^1];
        Assert.Equal((StepProgressPhase.End, StepStatus.Cancelled), (last.Phase, last.Status));

        finish.SetResult();
        await running;
        Assert.Equal(StepStatus.Cancelled, test.All()[0].Status);
        await session.RunAsync(StepKind.App, "app.open", "/late", () => Task.CompletedTask);
        Assert.Single(test.All());
    }
}
