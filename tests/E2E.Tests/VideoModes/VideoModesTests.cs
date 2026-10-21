// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.VideoModes;

/// <summary>
/// <c>describe('video modes')</c> of upstream <c>video-modes.test.ts</c>: which attempts record a video and which videos are
/// kept, driven on the real web engine, whose videos are what a user watches.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class VideoModesTests : IDisposable
{
    // The output directory holds the results and the run's report.json beside them.
    private readonly string _results = Path.Combine(Directory.CreateTempSubdirectory("e2e-video-modes-").FullName, "results");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_results)!, recursive: true);

    /// <summary>
    /// Partial: the port has no runner, so each test is run by hand with its retries, with the modes already resolved
    /// (<c>--video</c> over the config is <c>RecordingSource.Run</c>, which the <c>Config</c> tests resolve), and it has no
    /// serial group, so the <c>wizard</c> group is not run. The report is the session's own <c>Videos</c>, and an attempt
    /// that never started a video must not stop one: a clean dispose says so.
    /// </summary>
    [Fact]
    public async Task Records_retries_only_under_on_all_retries_video_wins_over_the_config_and_a_test_mode_wins_over_the_flag_PARTIAL_no_runner_report_or_serial_group()
    {
        using var app = await FixtureApp.StartAsync();
        var run = new ResolvedRecording(RecordingMode.OnAllRetries, RecordingSource.Run);
        Assert.Equal([0], await VideosPerAttemptAsync(app.Url, "passes first time", run, failures: 0));
        Assert.Equal([0, 1, 1], await VideosPerAttemptAsync(app.Url, "always fails", run, failures: 3));
        Assert.Equal([0, 1, 0], await VideosPerAttemptAsync(app.Url, "first retry only", new ResolvedRecording(RecordingMode.OnFirstRetry, RecordingSource.Test), failures: 3));
        Assert.Equal([0], await VideosPerAttemptAsync(app.Url, "kept on failure, passing", new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Test), failures: 0));
        Assert.Equal([1, 1, 1], await VideosPerAttemptAsync(app.Url, "kept on failure, failing", new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Test), failures: 3));
    }

    /// <summary>How many videos each attempt of a test kept, by attempt index: the test runs up to three attempts and stops at the first that passes, <paramref name="failures"/> of them failing.</summary>
    private async Task<List<int>> VideosPerAttemptAsync(string appUrl, string title, ResolvedRecording video, int failures)
    {
        var counts = new List<int>();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var fails = attempt <= failures;
            var session = await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = new WebEngine(),
                BaseUrl = appUrl,
                TestTitle = title,
                Attempt = attempt,
                Video = video,
                ResultsDirectory = _results,
                ProjectRoot = _results,
                CacheEnabled = false,
            });
            await session.App.OpenAsync();
            session.Complete(fails ? new TestException("TEST_FAILED", "nope") : null);
            await session.DisposeAsync();
            counts.Add(session.Videos.Count);
            if (!fails)
            {
                break;
            }
        }

        return counts;
    }
}
