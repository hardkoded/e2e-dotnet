// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;
using E2E.Engine;
using E2E.Reports;

namespace E2E.Tests.Output;

/// <summary>
/// <c>describe('ArtifactStore.putLink')</c> of upstream <c>output.test.ts</c>. Upstream runs a project through the runner over a fake
/// engine and reads <c>report.json</c>. The port has no run, so each body runs a session over a fake <see cref="IEngine"/> that records a
/// file and hands back a hosted link, and reads <c>E2ESession.Report()</c>. Both tests are partial for that reason. Not ported here: the
/// other <c>describe('output')</c> tests, which are about the run's output directory.
/// </summary>
public sealed class ArtifactStorePutLinkTests : IDisposable
{
    private const string Hosted = "https://recordings.example/r.mp4";

    private readonly string _results = Path.Combine(Directory.CreateTempSubdirectory("e2e-put-link-").FullName, "results");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_results)!, recursive: true);

    [Fact]
    public async Task Hands_a_hosted_recording_to_putLink_with_its_identity_and_records_the_ref_beside_the_url_PARTIAL_no_run_so_no_report_schema_check()
    {
        var capture = new CapturingStore();
        var session = await StartAsync(capture, new ResolvedRecording(RecordingMode.On, RecordingSource.Run));
        session.Complete();
        await session.DisposeAsync();

        var report = session.Report()!;
        var attempt = report.Run.Results[0].Attempts[0];
        var hosted = attempt.Artifacts.Single(artifact => artifact.Url == Hosted);
        Assert.Equal("store://link/1", hosted.Ref);
        var link = Assert.Single(capture.Links);
        Assert.Equal(
            new StoredArtifactLink
            {
                Url = Hosted,
                MediaType = "video/mp4",
                RunId = report.Run.Id,
                TestId = AttemptReport.TestId("", "taps a node"),
                AttemptId = attempt.Id,
                StartedAt = hosted.StartedAt!.Value,
            },
            link);
        Assert.Equal(ArtifactKind.Video, link.Kind);
        Assert.Equal(ArtifactRedaction.Incomplete, link.Redaction);

        // The file segment still goes to put, and the link never does.
        Assert.EndsWith("video/fake.webm", Assert.Single(capture.Puts, artifact => artifact.Kind == ArtifactKind.Video).Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Never_hands_over_a_passed_attempts_link_under_retain_on_failure_and_a_failing_putLink_fails_nothing_PARTIAL_no_run_status_or_cleanup_field()
    {
        var kept = new CapturingStore();
        var passed = await StartAsync(kept, new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Run));
        passed.Complete();
        await passed.DisposeAsync();
        Assert.Empty(kept.Links);

        var broken = new CapturingStore { FailLinks = true };
        var failing = await StartAsync(broken, new ResolvedRecording(RecordingMode.On, RecordingSource.Run));
        failing.Complete();
        await failing.DisposeAsync();
        Assert.Single(broken.Links);
        Assert.Equal(ReportRunStatus.Passed, failing.Report()!.Run.Status);
        var hosted = failing.Report()!.Run.Results[0].Attempts[0].Artifacts.Single(artifact => artifact.Url == Hosted);
        Assert.Null(hosted.Ref);
    }

    private Task<E2ESession> StartAsync(IArtifactStore store, ResolvedRecording video) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new FakeVideoEngine(),
            TestTitle = "taps a node",
            Video = video,
            Attempt = 1,
            ArtifactStore = store,
            ResultsDirectory = _results,
            ProjectRoot = _results,
        });

    private sealed class CapturingStore : IArtifactStore
    {
        private readonly ConcurrentQueue<StoredArtifact> _puts = new();
        private readonly ConcurrentQueue<StoredArtifactLink> _links = new();

        public bool FailLinks { get; init; }

        public IReadOnlyList<StoredArtifact> Puts => [.. _puts];

        public IReadOnlyList<StoredArtifactLink> Links => [.. _links];

        public Task<string> PutAsync(StoredArtifact artifact, CancellationToken cancellationToken = default)
        {
            _puts.Enqueue(artifact);
            return Task.FromResult("store://" + artifact.Kind + "/" + artifact.Sha256[..8]);
        }

        public Task<string?> PutLinkAsync(StoredArtifactLink link, CancellationToken cancellationToken = default)
        {
            _links.Enqueue(link);
            return FailLinks ? throw new InvalidOperationException("the bucket is down") : Task.FromResult<string?>("store://link/" + _links.Count);
        }
    }

    private sealed class FakeVideoEngine : IEngine
    {
        public string Platform => "fake";

        public string Version => "test";

        public EngineCapabilities Capabilities => EngineCapabilities.Observation | EngineCapabilities.Video;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
        {
            var session = DispatchProxy.Create<IEngineSession, SessionProxy>();
            ((SessionProxy)(object)session).ArtifactsDir = options.ArtifactsDir!;
            return Task.FromResult(session);
        }
    }

    public class SessionProxy : DispatchProxy
    {
        internal string ArtifactsDir { get; set; } = "";

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IEngineSession.StartVideoAsync):
                    return Task.CompletedTask;
                case nameof(IEngineSession.StopVideoAsync):
                    Directory.CreateDirectory(Path.Combine(ArtifactsDir, "video"));
                    File.WriteAllText(Path.Combine(ArtifactsDir, "video", "fake.webm"), "fake-vid");
                    IReadOnlyList<VideoSegment> segments =
                    [
                        new VideoLink { Url = Hosted, MediaType = "video/mp4", StartedAt = "2026-09-28T10:00:00.000Z" },
                        new VideoFile { Path = "video/fake.webm", StartedAt = "2026-09-28T10:00:00.000Z" },
                    ];
                    return Task.FromResult(segments);
                case nameof(IAsyncDisposable.DisposeAsync):
                    return default(ValueTask);
                case "get_" + nameof(IEngineSession.CanLocate):
                    return false;
                default:
                    return null;
            }
        }
    }
}
