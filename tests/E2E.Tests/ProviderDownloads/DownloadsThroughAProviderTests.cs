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
/// read. Here the "remote" disk is a local directory the provider reads.
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
        if (_session is not null)
        {
            await _session.DisposeAsync();
        }

        if (_worker is not null)
        {
            await _worker.DisposeAsync();
            await _worker.FinishAsync(ToyRunner.FinishInfo(), None);
        }

        if (_remote is not null)
        {
            await _remote.DisposeAsync();
        }

        foreach (var dir in _made)
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(BrowserProviderScope.Worker)]
    [InlineData(BrowserProviderScope.Attempt)]
    public async Task Saves_the_file_on_the_browsers_disk_and_reads_it_back_through_the_provider(BrowserProviderScope scope)
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
            () => {
              const link = document.createElement('a');
              link.href = URL.createObjectURL(new Blob(['report body']));
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

        var file = await browser.WaitForDownloadAsync(() => page.Locator("#download").ClickAsync());

        Assert.Equal("report.txt", file.SuggestedFilename);
        Assert.True(Path.IsPathRooted(file.AbsolutePath));
        Assert.Equal(Path.GetFullPath(Path.Combine(artifactsDir, file.Path)), file.AbsolutePath);
        Assert.Equal("report body", await File.ReadAllTextAsync(file.AbsolutePath));
        Assert.Matches(@"^downloads/\d{3}-report\.txt$", file.Path);
        Assert.Equal("report body", await File.ReadAllTextAsync(Path.Combine(artifactsDir, file.Path)));
        Assert.Equal([file.Path], attached);
        var read = Assert.Single(reads);
        Assert.Equal("lease-1", read.Lease);
        Assert.Equal(Path.Combine(remoteDisk, "downloads"), Path.GetDirectoryName(read.File));
        Assert.Equal([Path.GetFileName(read.File)], Directory.GetFiles(Path.Combine(remoteDisk, "downloads")).Select(Path.GetFileName));
    }
}
