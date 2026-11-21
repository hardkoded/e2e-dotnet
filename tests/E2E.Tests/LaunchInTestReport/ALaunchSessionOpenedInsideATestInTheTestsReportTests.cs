// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Reports;
using E2E.Tests.Core;
using R = E2E.Reports;

namespace E2E.Tests.LaunchInTestReport;

/// <summary>
/// Upstream: packages/e2e/tests/integration/launch-in-test-report.test.ts, describe('a launch() session opened inside a test, in the test's report'):
/// a test that opens a second session with <c>Launch.LaunchAsync</c>. The session's steps are in the test's record, each under the session's name and
/// in the order they began, its video is among the test's artifacts, and the attempt owns its end: a session the test left open, or left a step
/// running on, is closed with it. The <c>it</c> 'keeps the videos of two serial members' sessions apart' is not here: the port has no serial groups (issue #1989).
/// </summary>
[Collection(E2E.Tests.SessionSecrecy.SharedSecretsCollection.Name)]
public sealed class ALaunchSessionOpenedInsideATestInTheTestsReportTests : IAsyncLifetime
{
    private static readonly LaunchApp FakeApp = new() { Url = "http://127.0.0.1:4500" };

    private static readonly SemanticNode[] ConfirmScene = [Button("confirm", "Confirm")];

    private readonly List<StepProgressEvent> _events = [];
    private readonly List<CoreFakeEngine> _engines = [];
    private CoreFakeEngine? _left;
    private CoreFakeEngine? _workerFixtureEngine;
    private CoreFakeEngine? _fromHook;
    private Session? _hookSession;
    private Session? _timedOut;
    private string? _inTeardown;
    private int _tries;
    private RunOutcome _outcome = null!;
    private FixtureProject _project = null!;
    private IDisposable? _listening;
    private Host? _shared;
    private Host? _visitingHost;
    private Session? _visiting;

    private static SemanticNode Button(string reference, string name) =>
        new()
        {
            Ref = reference,
            Role = "button",
            Name = name,
        };

    private CoreFakeEngine Second(
        IReadOnlyList<string>? videoLinks = null,
        Func<Task>? onStartAttempt = null,
        bool track = true
    )
    {
        var engine = new CoreFakeEngine(ConfirmScene, video: true, videoLinks: videoLinks)
        {
            OnStartAttempt = onStartAttempt,
        };
        if (track)
        {
            lock (_engines)
            {
                _engines.Add(engine);
            }
        }

        return engine;
    }

    private static Task<Host> Launched(CoreFakeEngine engine, TimeSpan? timeout = null) =>
        Launch.LaunchAsync(
            (IEngine)engine,
            new LaunchOptions
            {
                App = FakeApp,
                Timeout = timeout ?? TimeSpan.FromSeconds(5),
                Env = new Dictionary<string, string?>(),
            }
        );

    private Task<Host> Open(IReadOnlyList<string>? videoLinks = null, Func<Task>? onStartAttempt = null) =>
        Launched(Second(videoLinks, onStartAttempt));

    private IReadOnlyList<InMemoryTest> Suite() =>
        [
            new InMemoryTest("drives a second session between its own steps", async session =>
            {
                await session.App.OpenAsync();
                var host = await Open();
                var second = await host.NewSessionAsync();
                await second.App.OpenAsync("/");
                await second.Screen.GetByRole("button", "Confirm").TapAsync();
                await host.CloseAsync();
                await session.Screen.GetByRole("button", "Send").TapAsync();
            }),
            new InMemoryTest("opens two more sessions", async session =>
            {
                await session.App.OpenAsync();
                var first = await Open();
                var other = await Open();
                var a = await first.NewSessionAsync();
                var b = await other.NewSessionAsync();
                await b.App.OpenAsync("/b");
                await a.App.OpenAsync("/a");
                await first.CloseAsync();
                await other.CloseAsync();
            }),
            new InMemoryTest("uses the host opened at the top of the file", async session =>
            {
                await session.App.OpenAsync();
                var second = await _shared!.NewSessionAsync();
                await second.App.OpenAsync("/shared");
                await second.CloseAsync();
            }),
            new InMemoryTest("uses it again", async session =>
            {
                await session.App.OpenAsync();
                var second = await _shared!.NewSessionAsync();
                await second.App.OpenAsync("/shared-again");
                await second.CloseAsync();
                await _shared.CloseAsync();
            }),
            new InMemoryTest("uses the session opened at the top of the file", async session =>
            {
                await session.App.OpenAsync();
                await _visiting!.App.OpenAsync("/visit");
            }),
            new InMemoryTest("uses that session again", async session =>
            {
                await session.App.OpenAsync();
                await _visiting!.App.OpenAsync("/visit-again");
                await _visitingHost!.CloseAsync();
            }),
            new InMemoryTest("passes while keeping videos of failures only", async session =>
            {
                await session.App.OpenAsync();
                var host = await Open();
                var second = await host.NewSessionAsync();
                await second.App.OpenAsync("/");
                await host.CloseAsync();
            }) { Video = RecordingMode.RetainOnFailure },
            new InMemoryTest("never closes the host and session it opened", async session =>
            {
                await session.App.OpenAsync();
                _left = Second(track: false);
                var host = await Launched(_left);
                var second = await host.NewSessionAsync();
                await second.App.OpenAsync("/left-open");
            }),
            new InMemoryTest("keeps the link of a session a hosted service recorded", async session =>
            {
                await session.App.OpenAsync();
                var host = await Open(videoLinks: ["https://videos.example.test/session-2"]);
                var second = await host.NewSessionAsync();
                await second.App.OpenAsync("/");
                await host.CloseAsync();
            }),
            new InMemoryTest("leaves a step of the second session running", async session =>
            {
                await session.App.OpenAsync();
                var host = await Open();
                var second = await host.NewSessionAsync();
                await second.App.OpenAsync("/");
                _ = second.Screen.GetByRole("button", "Never there").WaitForAsync();
            }),
            new InMemoryTest("never awaits the session it opens", async session =>
            {
                await session.App.OpenAsync();
                var host = await Open(onStartAttempt: () => Task.Delay(300));
                _ = host.NewSessionAsync();
            }),
            new InMemoryTest("fails a locator on the second session", async session =>
            {
                await session.App.OpenAsync();
                var host = await Launched(Second(), TimeSpan.FromMilliseconds(200));
                var second = await host.NewSessionAsync();
                await second.App.OpenAsync("/");
                await second.Screen.GetByRole("button", "Send").TapAsync();
            }),
            new InMemoryTest("uses the session a worker fixture opened", async session =>
            {
                await session.App.OpenAsync();
                await ((Session)Phone.Value!).App.OpenAsync("/worker-first");
            }) { WorkerFixtures = [Phone] },
            new InMemoryTest("uses that fixture again", async session =>
            {
                await session.App.OpenAsync();
                await ((Session)Phone.Value!).App.OpenAsync("/worker-second");
            }) { WorkerFixtures = [Phone] },
            new InMemoryTest("times out while a step of the second session waits", async session =>
            {
                await session.App.OpenAsync();
                var host = await Open();
                var second = await host.NewSessionAsync();
                _timedOut = second;
                await second.App.OpenAsync("/");
                await second.Screen.GetByRole("button", "Never there").WaitForAsync();
            })
            {
                Timeout = TimeSpan.FromSeconds(2),
                Video = RecordingMode.RetainOnFailure,
                // By teardown the session's step is cut and the session closed: nothing of it goes on driving the app.
                AfterEach =
                [
                    async () =>
                    {
                        try
                        {
                            await _timedOut!.App.OpenAsync("/in-teardown");
                            _inTeardown = "still open";
                        }
                        catch (E2EException cause)
                        {
                            _inTeardown = cause.Code;
                        }
                    },
                ],
            },
            new InMemoryTest("uses the session its hook opened", async session =>
            {
                await session.App.OpenAsync();
                await _hookSession!.App.OpenAsync("/from-the-hook");
            })
            {
                BeforeEach =
                [
                    async () =>
                    {
                        _fromHook = Second(track: false);
                        var host = await Launched(_fromHook);
                        _hookSession = await host.NewSessionAsync();
                    },
                ],
            },
            new InMemoryTest("passes on its retry", async session =>
            {
                await session.App.OpenAsync();
                var tries = Interlocked.Increment(ref _tries);
                var host = await Open();
                var second = await host.NewSessionAsync();
                await second.App.OpenAsync("/try-" + tries);
                await host.CloseAsync();
                if (tries == 1)
                {
                    throw new InvalidOperationException("the first try fails");
                }
            }) { Retries = 1 },
        ];

    // A worker fixture is set up inside the first test that names it and outlives it: what it launches is no test's to close.
    private FixtureDefinition Phone => _phone ??= new FixtureDefinition("phone", async use =>
        {
            var engine = new CoreFakeEngine(ConfirmScene, video: true);
            _workerFixtureEngine = engine;
            var host = await Launched(engine);
            await use(await host.NewSessionAsync());
            await host.CloseAsync();
        });

    private FixtureDefinition? _phone;

    private static string Config =>
        """{ "video": "on", "trace": "on", "actionTimeout": 300, "assertionTimeout": 200, "cleanupTimeout": 2000 }""";

    private static CoreFakeEngine TestEngine() => new([Button("send", "Send")], video: true);

    public async Task InitializeAsync()
    {
        // Opened while no test runs; each test opens its own session on it.
        _shared = await Open();
        // A session opened while no test runs: it visits the tests that use it, and is nobody's to close but the file's.
        _visitingHost = await Open();
        _visiting = await _visitingHost.NewSessionAsync();
        _project = RunProject.CreateProject();
        _listening = E2E.RunEvents.Subscribe(new RunEventSubscriber("launch-in-test-report", runEvent =>
            {
                if (runEvent.Fact is StepProgressEvent step)
                {
                    lock (_events)
                    {
                        _events.Add(step);
                    }
                }
            }));
        _outcome = await RunProject.RunExistingAsync(
            _project,
            new RunProjectOptions
            {
                Tests = Suite(),
                Engine = TestEngine(),
                Config = Config,
            }
        );
    }

    public Task DisposeAsync()
    {
        _listening?.Dispose();
        _project.Cleanup();
        return Task.CompletedTask;
    }

    private string Results => Path.Combine(_project.Dir, ".e2e", "results");

    private IReadOnlyList<R.ReportAttempt> AttemptsOf(string title) =>
        RunProject.ResultByTitle(_outcome, title).Attempts;

    private R.ReportAttempt AttemptOf(string title) => AttemptsOf(title)[0];

    private IReadOnlyList<R.ArtifactRecord> SessionVideos(string title) =>
        [
            .. AttemptOf(title)
                .Artifacts.Where(artifact => artifact.Kind == R.ArtifactKind.Video && artifact.Actor is not null),
        ];

    [Fact]
    public void Partial_Records_the_sessions_steps_with_the_tests_own_under_its_name_and_in_the_order_they_began()
    {
        // Not checked: that the session's step names the test line that called it, read against the test's project. The port's fixture project is a
        // temporary directory the test file is not in; the relocation is checked by the guest-steps tests. Not checked either: that the report is valid
        // against the schema, since the run's outcome is the typed report, which is not the wire document `assertValidReport` reads.
        var attempt = AttemptOf("drives a second session between its own steps");

        Assert.Equal(R.AttemptStatus.Passed, attempt.Status);
        Assert.Equal(
            [
                ((string?)null, "app.open", R.StepStatus.Passed),
                ("launch-1", "app.open", R.StepStatus.Passed),
                ("launch-1", "locator.tap", R.StepStatus.Passed),
                (null, "locator.tap", R.StepStatus.Passed),
            ],
            attempt.Steps.Select(step => (step.Actor, step.Api, step.Status))
        );
        Assert.Equal([0, 1, 2, 3], attempt.Steps.Select(step => step.Index));
        Assert.Equal(4, attempt.Steps.Select(step => step.Id).Distinct().Count());
    }

    [Fact]
    public void Tells_reporters_of_each_such_step_as_it_runs_with_the_sessions_name()
    {
        var attempt = AttemptOf("drives a second session between its own steps");
        var tapped = attempt.Steps[2];
        List<StepProgressEvent> heard;
        lock (_events)
        {
            heard = [.. _events];
        }

        var told = heard.Where(step => step.Progress.Identity?.StepId == tapped.Id).ToList();

        Assert.Contains(R.StepProgressPhase.Start, told.Select(step => step.Progress.Phase));
        Assert.Contains(R.StepProgressPhase.End, told.Select(step => step.Progress.Phase));
        foreach (var step in told)
        {
            Assert.Equal(
                ("launch-1", 2, attempt.Id),
                (step.Progress.Identity!.Actor, step.Progress.Identity.StepIndex, step.Progress.Identity.AttemptId)
            );
        }

        var own = heard.First(step => step.Progress.Identity?.StepId == attempt.Steps[0].Id);
        Assert.Null(own.Progress.Identity!.Actor);
    }

    [Fact]
    public void Names_each_session_in_the_order_the_test_opened_them()
    {
        Assert.Equal(
            [((string?)null, "/"), ("launch-2", "/b"), ("launch-1", "/a")],
            AttemptOf("opens two more sessions").Steps.Select(step => (step.Actor, step.Label))
        );
    }

    [Fact]
    public void Keeps_the_sessions_video_among_the_tests_artifacts_under_its_name()
    {
        var attempt = AttemptOf("drives a second session between its own steps");

        Assert.Equal(
            [(string?)null, "launch-1"],
            attempt.Artifacts.Where(artifact => artifact.Kind == R.ArtifactKind.Video).Select(video => video.Actor)
        );
        var kept = Assert.Single(SessionVideos("drives a second session between its own steps"));
        Assert.Matches(@"video/launch-1\.webm\z", kept.Path);
        Assert.NotNull(kept.StartedAt);
        Assert.True(File.Exists(Path.Combine(Results, kept.Path!)));
    }

    [Fact]
    public void Gives_each_test_that_opens_a_session_on_a_host_from_outside_it_that_sessions_steps_and_video()
    {
        foreach (
            var (title, label) in new[]
            {
                ("uses the host opened at the top of the file", "/shared"),
                ("uses it again", "/shared-again"),
            }
        )
        {
            Assert.Equal(
                [((string?)null, "/"), ("launch-1", label)],
                AttemptOf(title).Steps.Select(step => (step.Actor, step.Label))
            );
            Assert.Single(SessionVideos(title));
        }
    }

    [Fact]
    public void Drops_the_sessions_video_and_its_file_by_the_rule_that_drops_the_tests_own()
    {
        var attempt = AttemptOf("passes while keeping videos of failures only");

        Assert.Equal(R.AttemptStatus.Passed, attempt.Status);
        Assert.Contains(attempt.Steps, step => step.Actor == "launch-1");
        Assert.DoesNotContain(attempt.Artifacts, artifact => artifact.Kind == R.ArtifactKind.Video);
        // Every session video still on disk is one some attempt kept.
        var kept = _outcome
            .Report.Run.Results.SelectMany(result => result.Attempts)
            .SelectMany(each => each.Artifacts)
            .Where(artifact => artifact.Actor is not null && artifact.Path is not null)
            .Select(artifact => artifact.Path!)
            .Order(StringComparer.Ordinal);
        var onDisk = Directory
            .EnumerateFiles(Results, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(Results, file).Replace('\\', '/'))
            .Where(file => System.Text.RegularExpressions.Regex.IsMatch(file, @"/video/[^/]*launch-\d"))
            .Order(StringComparer.Ordinal);
        Assert.Equal(kept, onDisk);
    }

    [Fact]
    public void Closes_the_host_and_session_the_test_left_open_and_keeps_the_sessions_video()
    {
        var attempt = AttemptOf("never closes the host and session it opened");

        Assert.Equal(R.AttemptStatus.Passed, attempt.Status);
        Assert.Empty(attempt.SecondaryErrors);
        Assert.Equal(R.AttemptCleanup.Complete, attempt.Cleanup);
        Assert.Single(SessionVideos("never closes the host and session it opened"));
        var stats = _left!.Stats();
        Assert.Equal((1, 1, 1), (stats.AttemptsStarted, stats.AttemptsEnded, stats.Disposes));
    }

    [Fact]
    public void Records_the_steps_a_session_opened_outside_every_test_runs_in_each_test_that_uses_it_and_leaves_its_end_to_the_file()
    {
        foreach (
            var (title, label) in new[]
            {
                ("uses the session opened at the top of the file", "/visit"),
                ("uses that session again", "/visit-again"),
            }
        )
        {
            var attempt = AttemptOf(title);

            Assert.Equal(R.AttemptStatus.Passed, attempt.Status);
            Assert.Equal(
                [((string?)null, "/"), ("launch-1", label)],
                attempt.Steps.Select(step => (step.Actor, step.Label))
            );
            // No test opened it, so no test recorded it.
            Assert.Empty(SessionVideos(title));
        }
    }

    [Fact]
    public void Keeps_a_hosted_recording_of_the_session_as_a_link_under_its_name()
    {
        Assert.Equal(
            [("launch-1", "https://videos.example.test/session-2"), ("launch-1", "launch-1.webm")],
            SessionVideos("keeps the link of a session a hosted service recorded")
                .Select(video => (video.Actor!, video.Url ?? video.Path!.Split('/')[^1]))
        );
    }

    [Fact]
    public void Fails_the_test_for_a_step_of_the_session_the_body_left_running_and_closes_the_session_under_it()
    {
        var attempt = AttemptOf("leaves a step of the second session running");

        Assert.Equal("STEP_NOT_AWAITED", attempt.Error?.Code);
        Assert.Contains("locator.waitFor", attempt.Error!.Message, StringComparison.Ordinal);
        var last = attempt.Steps[^1];
        Assert.Equal(
            ("launch-1", "locator.waitFor", R.StepStatus.Failed, "STEP_NOT_AWAITED"),
            (last.Actor, last.Api, last.Status, last.Error?.Code)
        );
        // The step the test never awaited was cancelled and observed: nothing of it reaches another test or the run.
        Assert.Empty(attempt.SecondaryErrors);
        Assert.DoesNotContain(_outcome.Report.Run.Results.SelectMany(result => result.Attempts), each =>
            each.Error?.Code == "UNHANDLED_REJECTION"
        );
        Assert.Single(SessionVideos("leaves a step of the second session running"));
    }

    [Fact]
    public void Fails_the_test_for_an_opening_the_body_left_in_flight_and_cancels_it_without_a_stray_rejection()
    {
        var attempt = AttemptOf("never awaits the session it opens");

        Assert.Equal("STEP_NOT_AWAITED", attempt.Error?.Code);
        Assert.Contains("host.NewSessionAsync()", attempt.Error!.Message, StringComparison.Ordinal);
        Assert.Empty(attempt.SecondaryErrors);
        Assert.Empty(_outcome.Report.Run.Errors);
        Assert.DoesNotContain(_outcome.Report.Run.Results.SelectMany(result => result.Attempts), each =>
            each.Error?.Code == "UNHANDLED_REJECTION"
        );
    }

    [Fact]
    public void Partial_Shows_the_tests_own_screen_for_a_failure_on_the_second_session_without_matching_that_sessions_locator_against_it()
    {
        // The trace page's heading for the screen ("Screen of the test's own session at failure") belongs to the trace page the reporters write: not ported.
        var attempt = AttemptOf("fails a locator on the second session");

        Assert.Equal("LOCATOR_NOT_FOUND", attempt.Error?.Code);
        Assert.Equal(("launch-1", R.StepStatus.Failed), (attempt.Steps[^1].Actor, attempt.Steps[^1].Status));
        // The test's own screen has a "Send" button, the very one the other session's locator asked for.
        Assert.NotNull(attempt.Failure?.Screen);
        Assert.Null(attempt.Failure!.Candidates);
    }

    [Fact]
    public void Cuts_a_step_of_the_session_the_test_timed_out_under_at_once_and_keeps_the_video_of_the_failure()
    {
        var attempt = AttemptOf("times out while a step of the second session waits");

        Assert.Equal(R.AttemptStatus.TimedOut, attempt.Status);
        var cut = attempt.Steps.First(step => step.Api == "locator.waitFor");
        Assert.Equal(("launch-1", R.StepStatus.Cancelled), (cut.Actor, cut.Status));
        // Cut when the body timed out, well before the five seconds the session would have waited.
        Assert.True(cut.DurationMs < 4_000, $"the step ran {cut.DurationMs} ms");
        Assert.Single(SessionVideos("times out while a step of the second session waits"));
        // And cut before teardown ran, not only when the attempt was over.
        Assert.Equal("CANCELLED", _inTeardown);
    }

    [Fact]
    public void Leaves_a_host_a_worker_fixture_launched_to_the_worker_and_records_its_steps_in_each_test_that_uses_it()
    {
        foreach (
            var (title, label) in new[]
            {
                ("uses the session a worker fixture opened", "/worker-first"),
                ("uses that fixture again", "/worker-second"),
            }
        )
        {
            var attempt = AttemptOf(title);
            Assert.Equal(R.AttemptStatus.Passed, attempt.Status);
            Assert.Equal(
                [((string?)null, "/"), ("launch-1", label)],
                attempt.Steps.Select(step => (step.Actor, step.Label))
            );
            Assert.Empty(SessionVideos(title));
        }

        // One session served both tests, and ended when the worker did.
        var stats = _workerFixtureEngine!.Stats();
        Assert.Equal((1, 1, 1), (stats.AttemptsStarted, stats.AttemptsEnded, stats.Disposes));
    }

    [Fact]
    public void Owns_a_host_a_hook_of_the_test_launched_and_closes_it_with_the_test()
    {
        var attempt = AttemptOf("uses the session its hook opened");

        Assert.Equal(R.AttemptStatus.Passed, attempt.Status);
        Assert.Equal(
            [((string?)null, "/"), ("launch-1", "/from-the-hook")],
            attempt.Steps.Select(step => (step.Actor, step.Label))
        );
        var stats = _fromHook!.Stats();
        Assert.Equal((1, 1), (stats.AttemptsEnded, stats.Disposes));
    }

    [Fact]
    public void Records_each_attempts_own_session_when_the_test_retries()
    {
        var attempts = AttemptsOf("passes on its retry");

        Assert.Equal([R.AttemptStatus.Failed, R.AttemptStatus.Passed], attempts.Select(attempt => attempt.Status));
        Assert.Equal(
            [
                ["/try-1"],
                ["/try-2"],
            ],
            attempts.Select(attempt =>
                attempt.Steps.Where(step => step.Actor == "launch-1").Select(step => step.Label).ToList()
            )
        );
    }
}
