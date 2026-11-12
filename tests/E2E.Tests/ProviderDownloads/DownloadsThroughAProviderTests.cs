// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.BrowserProvider;
using ToyRunner = E2E.Tests.BrowserProvider.Runner;

namespace E2E.Tests.ProviderDownloads;

/// <summary>
/// <c>describe.each(['worker', 'attempt'])('downloads through a provider, %s scope')</c> of upstream
/// <c>provider-downloads.test.ts</c>: a provider that serves downloads, against a real Chrome over CDP. The browser saves the file to
/// the provider's directory on its own disk, not the runner's, and <c>browser.waitForDownload</c> gets it back through the provider's
/// read. Here the "remote" disk is a local directory the provider reads. A trigger can start several files, which
/// <c>browser.waitForDownloads</c> collects, and one timeout bounds a read that stalls.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class DownloadsThroughAProviderTests : IAsyncLifetime
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly List<string> _made = [];
    private RemoteChrome? _remote;
    private WebEngine? _worker;
    private IEngineSession? _session;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Every step runs, whatever the earlier ones did, so a failed test never leaves a Chrome process or a directory behind.
        try
        {
            if (_session is not null)
            {
                await _session.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                if (_worker is not null)
                {
                    try
                    {
                        await _worker.DisposeAsync();
                    }
                    finally
                    {
                        await _worker.FinishAsync(ToyRunner.FinishInfo(), None);
                    }
                }
            }
            finally
            {
                try
                {
                    if (_remote is not null)
                    {
                        await _remote.DisposeAsync();
                    }
                }
                finally
                {
                    foreach (var dir in _made)
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(BrowserProviderScope.Worker, 1)]
    [InlineData(BrowserProviderScope.Worker, 2)]
    [InlineData(BrowserProviderScope.Attempt, 1)]
    [InlineData(BrowserProviderScope.Attempt, 2)]
    public async Task Saves_N_files_on_the_browser_disk_through_one_trigger(BrowserProviderScope scope, int count)
    {
        using var app = await FixtureApp.StartAsync();
        var artifactsDir = Directory.CreateTempSubdirectory("e2e-provider-downloads-").FullName;
        var remoteDisk = Directory.CreateTempSubdirectory("e2e-browser-disk-").FullName;
        _made.AddRange([artifactsDir, remoteDisk]);
        _remote = await RemoteChrome.LaunchAsync();
        var reads = new List<(string Lease, string File)>();
        var cloud = new ToyCloud(scope)
        {
            OnAcquire = (request, token) => Task.FromResult<BrowserLease>(new ToyLease { Id = "lease-1", CdpEndpoint = _remote.Endpoint }),
            Downloads = new BrowserProviderDownloads
            {
                Dir = Path.Combine(remoteDisk, "downloads"),
                ReadAsync = (lease, file, context, token) =>
                {
                    reads.Add((lease.Id, file));
                    return File.ReadAllBytesAsync(file, token);
                },
            },
        };
        _worker = new WebEngine(new WebEngineOptions { Browser = cloud });
        var prepared = await _worker.PrepareAsync(ToyRunner.PrepareInfo(1), None);
        _worker.Init(ToyRunner.InitInfo(0, ToyRunner.Widen(prepared.Env)));
        _session = await _worker.StartAsync(new EngineStartOptions { AttemptId = "a1", ArtifactsDir = artifactsDir, ActionTimeout = TimeSpan.FromSeconds(15) }, None);
        await _session.OpenAsync(app.Url + "/login", None);
        var page = WebEngine.SurfaceOf(_session)!.Page();
        await page.EvaluateAsync("""
            (downloadCount) => {
              for (let index = 0; index < downloadCount; index += 1) {
                const link = document.createElement('a');
                link.href = URL.createObjectURL(new Blob([`report body ${index}`]));
                link.download = `report-${index}.txt`;
                link.id = `download-${index}`;
                link.textContent = 'Download';
                document.body.append(link);
              }
            }
            """, count);
        var attached = new List<string>();
        var browser = new Browser(_session, "web", app.Url, artifactsDir, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15), () => None, static () => { }, new SoftFailures())
        {
            Attach = (_, relative) => attached.Add(relative),
        };

        var triggers = 0;
        async Task Trigger()
        {
            triggers++;
            for (var index = 0; index < count; index++)
            {
                await page.Locator($"#download-{index}").ClickAsync();
            }
        }

        IReadOnlyList<BrowserDownload> files = count == 1
            ? [await browser.WaitForDownloadAsync(Trigger)]
            : await browser.WaitForDownloadsAsync(Trigger, count);
        Assert.Equal(1, triggers);
        Assert.Equal(count, files.Count);
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            Assert.Equal($"report-{index}.txt", file.SuggestedFilename);
            Assert.True(Path.IsPathRooted(file.AbsolutePath));
            Assert.Equal(Path.GetFullPath(Path.Combine(artifactsDir, file.Path)), file.AbsolutePath);
            Assert.Equal($"report body {index}", await File.ReadAllTextAsync(file.AbsolutePath));
            Assert.Matches($@"^downloads/\d{{3}}-report-\d\.txt$", file.Path);
        }

        Assert.Equal(files.Select(file => file.Path), attached);
        Assert.Equal(count, reads.Count);
        Assert.All(reads, read => Assert.Equal("lease-1", read.Lease));
        Assert.All(reads, read => Assert.Equal(Path.Combine(remoteDisk, "downloads"), Path.GetDirectoryName(read.File)));
        Assert.Equal(
            reads.Select(read => Path.GetFileName(read.File)).Order(StringComparer.Ordinal),
            Directory.GetFiles(Path.Combine(remoteDisk, "downloads")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(BrowserProviderScope.Worker, "waitForDownload")]
    [InlineData(BrowserProviderScope.Worker, "waitForDownloads")]
    [InlineData(BrowserProviderScope.Attempt, "waitForDownload")]
    [InlineData(BrowserProviderScope.Attempt, "waitForDownloads")]
    public async Task Bounds_a_completed_download_whose_provider_read_stalls(BrowserProviderScope scope, string method)
    {
        using var app = await FixtureApp.StartAsync();
        var artifactsDir = Directory.CreateTempSubdirectory("e2e-provider-stalled-").FullName;
        var remoteDisk = Directory.CreateTempSubdirectory("e2e-browser-stalled-").FullName;
        _made.AddRange([artifactsDir, remoteDisk]);
        _remote = await RemoteChrome.LaunchAsync();
        CancellationToken? readSignal = null;
        var cloud = new ToyCloud(scope)
        {
            OnAcquire = (request, token) => Task.FromResult<BrowserLease>(new ToyLease { Id = "stalled-lease", CdpEndpoint = _remote.Endpoint }),
            Downloads = new BrowserProviderDownloads
            {
                Dir = Path.Combine(remoteDisk, "downloads"),
                ReadAsync = async (lease, file, context, token) =>
                {
                    Assert.Equal("stalled provider body", await File.ReadAllTextAsync(file, token));
                    readSignal = token;
                    await Task.Delay(Timeout.Infinite, token);
                    return [];
                },
            },
        };
        _worker = new WebEngine(new WebEngineOptions { Browser = cloud });
        var prepared = await _worker.PrepareAsync(ToyRunner.PrepareInfo(1), None);
        _worker.Init(ToyRunner.InitInfo(0, ToyRunner.Widen(prepared.Env)));
        _session = await _worker.StartAsync(new EngineStartOptions { AttemptId = "a1", ArtifactsDir = artifactsDir, ActionTimeout = TimeSpan.FromSeconds(15) }, None);
        await _session.OpenAsync(app.Url, None);
        var page = WebEngine.SurfaceOf(_session)!.Page();
        await page.EvaluateAsync("""
            () => {
              const link = document.createElement('a');
              link.href = URL.createObjectURL(new Blob(['stalled provider body']));
              link.download = 'report.txt';
              link.id = 'download';
              link.textContent = 'Download';
              document.body.append(link);
            }
            """);
        var attached = new List<string>();
        var browser = new Browser(_session, "web", app.Url, artifactsDir, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15), () => None, static () => { }, new SoftFailures())
        {
            Attach = (_, relative) => attached.Add(relative),
        };

        Task Trigger() => page.Locator("#download").ClickAsync();
        Task work = method == "waitForDownload"
            ? browser.WaitForDownloadAsync(Trigger, TimeSpan.FromMilliseconds(500))
            : browser.WaitForDownloadsAsync(Trigger, 1, TimeSpan.FromMilliseconds(500));
        var finished = await Task.WhenAny(work, Task.Delay(1500));

        Assert.NotNull(readSignal);
        Assert.Same(work, finished);
        var failure = await Assert.ThrowsAsync<TestException>(() => work);
        Assert.Equal("ACTION_FAILED", failure.Code);
        Assert.Contains("did not finish", failure.Message, StringComparison.Ordinal);
        Assert.True(readSignal!.Value.IsCancellationRequested);
        Assert.Empty(attached);
    }
}
