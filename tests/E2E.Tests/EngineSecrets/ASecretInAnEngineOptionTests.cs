// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using E2E.Engine;
using E2E.Reports;

namespace E2E.Tests.EngineSecrets;

/// <summary>
/// Ported from upstream's <c>engine-secrets.test.ts</c>. The handle comes from <see cref="Secret.Create"/>, since the port
/// has no <c>e2e.config.ts</c> to call <c>secrets.get()</c>. Not ported, because the port has no such feature: "redacts the
/// value the page echoes from the failure, the report, and the reporters' files" and "redacts the base64 credential of the
/// echoed header everywhere" (an engine option does not add its value to the session's redaction list, and there are no
/// reporters), and "hands the engine a declared value and refuses one it did not declare" (the engine has no declared-secrets API).
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ASecretInAnEngineOptionTests
{
    private const string Password = "basic-Pa55-7Qz";

    /// <summary>
    /// Partial: the port has no step record, so it cannot read <c>visionInput</c> and <c>visionDegraded</c>. It reads the image
    /// in the request, which an engine-held secret does not withhold, and the credential in none of it.
    /// </summary>
    [Fact]
    public async Task Sends_the_tree_without_the_credential_and_its_pixels_as_with_no_secret()
    {
        using var site = await TinySite.StartAsync(async context =>
        {
            var header = context.Request.Headers["Authorization"];
            if (header is null)
            {
                context.Response.StatusCode = 401;
                context.Response.AddHeader("WWW-Authenticate", "Basic realm=\"staging\"");
                context.Response.Close();
                return;
            }

            await TinySite.RespondAsync(context, $"<!doctype html><html><head><title>Echo</title></head><body><h1>Echo</h1><p>Authorization: {header}</p></body></html>");
        });
        var judge = new ScriptedModel(_ => ModelResponses.Done("passed", "it does"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(new WebEngineOptions
            {
                Headless = true,
                BasicAuth = new WebBasicAuth("ada", Secret.Create("stagingPassword", Password)),
            }),
            Judge = judge,
            BaseUrl = site.Url,
            CacheEnabled = false,
            TestTitle = "judges the echo with vision on",
            AssertionTimeout = TimeSpan.FromSeconds(4),
            TestTimeout = TimeSpan.FromSeconds(30),
            ResultsDirectory = Path.Combine(Path.GetTempPath(), "e2e-echo-" + Guid.NewGuid().ToString("N"), "results"),
        });
        await session.App.OpenAsync("/basic-auth");
        await session.Agent.AssertAsync("the page shows a Basic credential", new AssertOptions { Vision = VisionMode.On });

        var judged = judge.Requests[0].Messages[0];
        var credential = Convert.ToBase64String(Encoding.UTF8.GetBytes("ada:" + Password));
        Assert.Contains("Basic <secret:stagingPassword>", judged.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(credential, judged.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, judged.Content, StringComparison.Ordinal);
        Assert.Single(judged.Images!);
    }

    /// <summary>
    /// Upstream runs one suite of five tests and reads three results; the port runs the three tests it reads, one session each, against
    /// one site that echoes the request headers the way upstream's <c>/basic-auth</c> fixture page does.
    /// </summary>
    [Fact]
    public async Task Protects_the_secret_as_text_only_screenshots_are_kept_the_text_download_rewritten()
    {
        using var site = await TinySite.StartAsync(async context =>
        {
            var header = context.Request.Headers["Authorization"];
            if (header is null)
            {
                context.Response.StatusCode = 401;
                context.Response.AddHeader("WWW-Authenticate", "Basic realm=\"staging\"");
                context.Response.Close();
                return;
            }

            if (context.Request.Url!.AbsolutePath == "/headers.txt")
            {
                var bytes = Encoding.UTF8.GetBytes("authorization: " + header + "\n");
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.AddHeader("Content-Disposition", "attachment; filename=\"headers.txt\"");
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
                return;
            }

            await TinySite.RespondAsync(
                context,
                $"<!doctype html><html><head><title>Basic auth</title></head><body><h1>Signed in as ada</h1><p data-testid=\"header\">Authorization: {header}</p><a href=\"/headers.txt\" download>Download headers</a></body></html>");
        });

        var shot = await RunAsync(site, "takes a screenshot of the page with an engine-held secret", async session =>
        {
            await session.App.OpenAsync("/basic-auth");
            await session.App.ScreenshotAsync("echo");
        });
        var failure = await RunAsync(site, "fails on the echoed Authorization header", async session =>
        {
            await session.App.OpenAsync("/basic-auth");
            await Expect.That(session.Screen.GetByTestId("header")).ToHaveTextAsync("something else", timeout: TimeSpan.FromMilliseconds(500));
        });
        var download = await RunAsync(site, "downloads the echoed headers", async session =>
        {
            await session.App.OpenAsync("/basic-auth");
            await session.Browser.WaitForDownloadAsync(() => session.Browser.Locator("a[download]").TapAsync());
        });

        Assert.True(shot.Failure is null, shot.Failure);
        Assert.Equal(E2E.Reports.ResultStatus.Passed, shot.Report.Run.Results[0].Status);
        var failed = failure.Report.Run.Results[0].Attempts[0];
        Assert.Single(failed.Artifacts, artifact => artifact.Kind == ArtifactKind.Screenshot);
        var downloaded = download.Report.Run.Results[0].Attempts[0].Artifacts.First(artifact => artifact.Kind == ArtifactKind.Download);
        Assert.Equal(ArtifactRedaction.Complete, downloaded.Redaction);
    }

    [Fact]
    public async Task Answers_the_challenge_with_the_resolved_value()
    {
        using var site = await TinySite.StartAsync(async context =>
        {
            var header = context.Request.Headers["Authorization"];
            if (header is null)
            {
                context.Response.StatusCode = 401;
                context.Response.AddHeader("WWW-Authenticate", "Basic realm=\"staging\"");
                context.Response.Close();
                return;
            }

            var credential = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..]));
            var user = credential[..credential.IndexOf(':', StringComparison.Ordinal)];
            await TinySite.RespondAsync(context, $"<!doctype html><html><head><title>Basic auth</title></head><body><h1>Signed in as {user}</h1></body></html>");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(new WebEngineOptions
            {
                Headless = true,
                BasicAuth = new WebBasicAuth("ada", Secret.Create("stagingPassword", Password)),
            }),
            BaseUrl = site.Url,
            CacheEnabled = false,
            TestTitle = "signs in through basic auth",
            AssertionTimeout = TimeSpan.FromSeconds(4),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
        await session.App.OpenAsync("/basic-auth");
        await Expect.That(session.Screen.GetByRole("heading", "Signed in as ada")).ToBeVisibleAsync();
    }

    private static async Task<(Report Report, string? Failure)> RunAsync(TinySite site, string title, Func<E2ESession, Task> scenario)
    {
        var results = Path.Combine(Path.GetTempPath(), "e2e-engine-secrets-" + Guid.NewGuid().ToString("N"), "results");
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(new WebEngineOptions
            {
                Headless = true,
                BasicAuth = new WebBasicAuth("ada", Secret.Create("stagingPassword", Password)),
            }),
            BaseUrl = site.Url,
            CacheEnabled = false,
            TestTitle = title,
            AssertionTimeout = TimeSpan.FromSeconds(4),
            TestTimeout = TimeSpan.FromSeconds(30),
            ResultsDirectory = results,
        });
        string? failure = null;
        try
        {
            await scenario(session);
            session.Complete();
        }
        catch (Exception ex)
        {
            failure = ex.ToString();
        }

        await session.DisposeAsync();
        return (session.Report() ?? throw new InvalidOperationException("the session left no report: " + failure), failure);
    }
}
