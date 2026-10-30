// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Internal;
using E2E.Reports;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;
using WebFixtureApp = E2E.Tests.WebPlatform.FixtureApp;

namespace E2E.Tests.FilledSecrets;

/// <summary>
/// <c>describe('filled secrets')</c> of upstream <c>filled-secrets.test.ts</c>. A filled secret never leaves the runner in text: a
/// download, the cache, or a trace page. Through the real web engine: a text download is rewritten, labelled <c>complete</c>, and handed
/// to the store already clean whenever the session knows the secret. The executor's fill is recorded in the trace cache by the secret's
/// name alone. Nothing under the project's <c>.e2e</c> directory holds the plaintext afterwards, a trace page for every test included.
/// The six tests of upstream's suite run once, in <see cref="FilledSecretsRun"/>, and the tests below read what they left.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class FilledSecretsTests(FilledSecretsRun run) : IClassFixture<FilledSecretsRun>
{
    [Fact]
    public void Passes_every_test()
    {
        Assert.True(run.Failures.IsEmpty, string.Join('\n', run.Failures.Select(failure => failure.Key + ": " + failure.Value)));
        Assert.Equal(FilledSecretsRun.Titles.Count, run.Reports.Count);
    }

    [Fact]
    public void Keeps_a_screenshot_after_a_fill()
    {
        var attempt = run.AttemptOf("fills a visible field");
        var screenshot = Assert.Single(attempt.Artifacts, artifact => artifact.Kind == ArtifactKind.Screenshot);
        Assert.Matches(new Regex(@"after-fill.*\.png\z", RegexOptions.None, TimeSpan.FromSeconds(2)), screenshot.Path);
    }

    [Fact]
    public void Downloads_after_a_fill_a_text_download_is_rewritten_labelled_complete_and_stored_clean()
    {
        var attempt = run.AttemptOf("downloads after a fill");
        var download = attempt.Artifacts.First(artifact => artifact.Kind == ArtifactKind.Download);
        Assert.Equal(ArtifactRedaction.Complete, download.Redaction);
        Assert.Equal("text/csv", download.MediaType);
        var onDisk = File.ReadAllText(Path.Combine(run.ResultsDir, download.Path!), new UTF8Encoding(false));
        Assert.Equal("id,key\n1,<secret:member.password>\n", onDisk);
        var put = run.Store.Puts.First(stored => stored.Path == download.Path);
        Assert.Equal(ArtifactKind.Download, put.Kind);
        Assert.Equal(ArtifactRedaction.Complete, put.Redaction);
        Assert.Equal(download.Sha256, put.Sha256);
        Assert.Equal(onDisk, Encoding.UTF8.GetString(put.Bytes.Span));
    }

    // PARTIAL: upstream declares the credential in the config, so the session scans a download whether or not the test filled it. The
    // port has no `credentials` config key, and a session learns a secret only when a test fills or passes it: with none, the download
    // is bytes the app served, labelled incomplete.
    [Fact]
    public void Downloads_without_a_fill_the_text_file_is_kept_as_served_PARTIAL()
    {
        var attempt = run.AttemptOf("downloads without a fill");
        var download = attempt.Artifacts.First(artifact => artifact.Kind == ArtifactKind.Download);
        Assert.Equal(ArtifactRedaction.Incomplete, download.Redaction);
        Assert.Equal("text/csv", download.MediaType);
        Assert.Equal("id,total\n1,42\n", File.ReadAllText(Path.Combine(run.ResultsDir, download.Path!), new UTF8Encoding(false)));
        Assert.Equal(ArtifactRedaction.Incomplete, run.Store.Puts.First(stored => stored.Path == download.Path).Redaction);
    }

    [Fact]
    public void Records_the_executor_fill_in_the_cache_by_the_secret_name_alone_with_no_value()
    {
        var entries = run.CacheEntries();
        var entry = Assert.Single(entries);
        Assert.Equal("enter the password", entry.Instruction);
        Assert.Collection(
            entry.Actions,
            fill =>
            {
                // The port records a secret fill as a `fill` whose value is the secret's marker, where upstream's `typeSecret` names the secret.
                Assert.Equal("fill", fill.Kind);
                Assert.Equal("textbox", fill.Role);
                Assert.Equal("Password", fill.Name);
                Assert.Equal("<secret:member.password>", fill.Value);
            },
            tap =>
            {
                Assert.Equal("tap", tap.Kind);
                Assert.Equal("button", tap.Role);
                Assert.Equal("Increment", tap.Name);
            });
    }

    [Fact]
    public void Leaves_the_plaintext_nowhere_under_e2e_in_the_report_or_in_what_the_store_received()
    {
        Assert.DoesNotContain(FilledSecretsRun.Secret, JsonSerializer.Serialize(run.Reports.Values.ToArray()), StringComparison.Ordinal);
        var plaintext = Encoding.UTF8.GetBytes(FilledSecretsRun.Secret);
        foreach (var put in run.Store.Puts)
        {
            Assert.False(put.Bytes.Span.IndexOf(plaintext) >= 0, put.Path);
        }

        var written = Directory.EnumerateFiles(run.E2eDir, "*", SearchOption.AllDirectories).ToList();
        Assert.Equal(FilledSecretsRun.Titles.Count, written.Count(file => file.EndsWith("trace.md", StringComparison.Ordinal)));
        foreach (var file in written)
        {
            Assert.False(File.ReadAllBytes(file).AsSpan().IndexOf(plaintext) >= 0, file);
        }
    }
}

/// <summary>
/// Upstream's <c>beforeAll</c> of <c>filled-secrets.test.ts</c>: the project, the fixture app, and the run of the suite's six tests with
/// a store, a trace on every test, and the replay cache read-write.
/// </summary>
public sealed class FilledSecretsRun : IAsyncLifetime
{
    internal const string Secret = "filled-secret-Qx7#\"&=2718";

    internal static readonly IReadOnlyList<string> Titles =
    [
        "fills through screen",
        "fills through the executor",
        "fills nothing",
        "downloads after a fill",
        "downloads without a fill",
        "fills a visible field",
    ];

    private readonly string _project = Directory.CreateTempSubdirectory("e2e-filled-secrets-").FullName;
    private TinySite? _site;

    public ArtifactStoreSpy Store { get; } = new();

    public ConcurrentDictionary<string, string> Failures { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, Report> Reports { get; } = new(StringComparer.Ordinal);

    public string E2eDir => Path.Combine(_project, ".e2e");

    public string ResultsDir => Path.Combine(E2eDir, "results");

    public E2E.Reports.ReportAttempt AttemptOf(string title) => Reports[title].Run.Results[0].Attempts[0];

    public IReadOnlyList<CacheEntry> CacheEntries()
    {
        var directory = Path.Combine(E2eDir, "cache");
        return [.. Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories).Select(file => JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(file), JsonDefaults.Options)!)];
    }

    public async Task InitializeAsync()
    {
        _site = await WebFixtureApp.StartAsync();
        foreach (var title in Titles)
        {
            await RunAsync(title);
        }
    }

    public Task DisposeAsync()
    {
        _site?.Dispose();
        Directory.Delete(_project, recursive: true);
        return Task.CompletedTask;
    }

    private async Task RunAsync(string title)
    {
        var password = global::E2E.Secret.Create("member.password", Secret, "password");
        var executor = new TestExecutor("secret-filler", async (context, token) =>
        {
            var observation = await context.ObserveAsync(new ExecutorObserveOptions { Tree = true }, token);
            ExecutorNode Find(Func<ExecutorNode, bool> match) => ExecutorTree.FindNode(observation.Tree, match)!;

            // Declared secrets are keyed by the credential's name, not the param's.
            await context.Actions.TypeSecretAsync(new ExecutorTarget(Find(node => node.States.Secure).Id), "member.password", token);

            // A secure field never shows its value, so the fill alone leaves nothing a replay could check; the counter is the step's
            // visible effect, which makes it recordable.
            await context.Actions.TapAsync(new ExecutorTarget(Find(node => node.Role == "button" && node.Name == "Increment").Id), token);
            return Passed("filled");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            AgentExecutor = executor,
            BaseUrl = _site!.Url,
            ProjectRoot = _project,
            ResultsDirectory = ResultsDir,
            Cache = new FileStepCache(Path.Combine(E2eDir, "cache")),
            CacheMode = CacheMode.ReadWrite,

            // A page for every test, passing ones too, so the scan reads each step's screen and log.
            Trace = new ResolvedRecording(RecordingMode.On, RecordingSource.Target),
            ArtifactStore = Store,
            TestFile = typeof(FilledSecretsTests).FullName,
            TestTitle = title,
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(30),
            TestTimeout = TimeSpan.FromSeconds(60),
        });
        Exception? failure = null;
        try
        {
            await ScenarioAsync(session, title, password);
            session.Complete();
        }
        catch (Exception ex)
        {
            failure = ex;
            Failures[title] = ex.ToString();
        }

        await session.DisposeAsync();
        if (session.Report() is { } report)
        {
            Reports[title] = report;
        }
        else if (failure is null)
        {
            Failures[title] = "the session left no report";
        }
    }

    private static async Task ScenarioAsync(E2ESession session, string title, Secret password)
    {
        var app = session.App;
        var screen = session.Screen;
        switch (title)
        {
            case "fills through screen":
                await app.OpenAsync();
                await screen.GetByLabel("Password").FillAsync(password);
                break;
            case "fills through the executor":
                await app.OpenAsync();
                await session.Agent.ActAsync("enter the password", new ActOptions { Params = new Dictionary<string, object?> { ["password"] = password } });
                await Expect.That(screen.GetByLabel("Password")).ToBeVisibleAsync();
                break;
            case "fills nothing":
                await app.OpenAsync();
                await screen.GetByLabel("Focus target").FillAsync("plain text");
                break;
            case "downloads after a fill":
                await app.OpenAsync("/exports");
                await screen.GetByLabel("Export key").FillAsync(password);
                await session.Browser.WaitForDownloadAsync(() => screen.GetByRole("link", "Download export").TapAsync());
                break;
            case "downloads without a fill":
                await app.OpenAsync("/downloads");
                await session.Browser.WaitForDownloadAsync(() => screen.GetByRole("link", "Download report").TapAsync());
                break;
            case "fills a visible field":
                await app.OpenAsync();
                await screen.GetByLabel("Focus target").FillAsync(password);
                await app.ScreenshotAsync("after-fill");
                break;
            default:
                throw new InvalidOperationException(title);
        }
    }
}

/// <summary>Upstream's <c>capturing()</c> of <c>filled-secrets.test.ts</c>: a store that keeps what it was handed.</summary>
public sealed class ArtifactStoreSpy : IArtifactStore
{
    private readonly ConcurrentQueue<StoredArtifact> _puts = new();

    public IReadOnlyList<StoredArtifact> Puts => [.. _puts];

    public Task<string> PutAsync(StoredArtifact artifact, CancellationToken cancellationToken = default)
    {
        _puts.Enqueue(artifact);
        return Task.FromResult("store://" + artifact.Sha256[..12]);
    }
}
