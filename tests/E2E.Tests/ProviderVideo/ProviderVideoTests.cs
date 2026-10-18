// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AttemptSession;
using E2E.Tests.BrowserProvider;
using Microsoft.Playwright;

namespace E2E.Tests.ProviderVideo;

/// <summary>
/// <c>describe('ProviderVideo')</c> of upstream <c>provider-video.test.ts</c>: the attempt's video as the browser provider
/// records it, through starts that outlive their budget and stops that fail.
/// </summary>
public sealed class ProviderVideoTests : IDisposable
{
    private static readonly IPage Page = Fake<IPage>.New().Proxy;

    private readonly List<string> _made = [];

    public void Dispose()
    {
        foreach (var dir in _made)
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Stops_a_start_that_landed_after_its_budget_ran_out_and_keeps_the_recording_started_after_it()
    {
        var stopped = new List<string>();
        var landing = new TaskCompletionSource<LeaseRecording?>();
        var starts = new Queue<Func<Task<LeaseRecording?>>>([
            () => landing.Task,
            () => Task.FromResult<LeaseRecording?>(Recording("second", stopped)),
        ]);
        var video = new E2E.Engine.ProviderVideo(_ => starts.Dequeue()(), ArtifactsDir());
        using var timedOut = new CancellationTokenSource();
        var first = video.ArmAsync(Page, timedOut.Token);
        await timedOut.CancelAsync();
        await video.ArmAsync(Page, CancellationToken.None);
        landing.SetResult(Recording("first", stopped));
        Assert.Equal("CANCELLED", (await Assert.ThrowsAsync<EngineException>(() => first)).Code);
        Assert.Equal(["first"], stopped);
        Assert.Equal(["video/second.mp4"], (await video.StopAsync(CancellationToken.None)).Select(segment => segment is VideoFile file ? file.Path : ((VideoLink)segment).Url));
    }

    [Fact]
    public async Task Keeps_the_handle_of_a_recording_whose_stop_failed_on_abandon_so_a_later_stop_still_ends_it()
    {
        var stopped = new List<string>();
        var video = new E2E.Engine.ProviderVideo(_ => Task.FromResult<LeaseRecording?>(Recording("clip", stopped, failures: 1)), ArtifactsDir());
        await video.ArmAsync(Page, CancellationToken.None);
        await video.AbandonAsync(CancellationToken.None);
        Assert.Equal(["clip"], stopped);
        Assert.Single(await video.StopAsync(CancellationToken.None));
        Assert.Equal(["clip", "clip"], stopped);
    }

    private string ArtifactsDir()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-provider-video-").FullName;
        _made.Add(dir);
        return dir;
    }

    /// <summary>A recording named <paramref name="name"/> whose stop writes <c>&lt;name&gt;.mp4</c>, failing the first <paramref name="failures"/> stops.</summary>
    private static LeaseRecording Recording(string name, List<string> stopped, int failures = 0)
    {
        var left = failures;
        var provided = new ToyRecording("2026-09-28T10:00:00.000Z", async dir =>
        {
            stopped.Add(name);
            if (left-- > 0)
            {
                throw new InvalidOperationException("recorder unreachable");
            }

            await File.WriteAllTextAsync(Path.Combine(dir, name + ".mp4"), "mp4", CancellationToken.None);
            return new ProviderRecordingResult { File = name + ".mp4" };
        });
        return new LeaseRecording { Recording = provided, Provider = "browser provider \"cloud\"", LeaseId = "lease-0" };
    }
}
