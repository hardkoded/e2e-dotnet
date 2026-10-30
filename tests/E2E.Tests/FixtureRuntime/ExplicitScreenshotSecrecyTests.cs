// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Tests.FixtureRuntime;

/// <summary>
/// Upstream's <c>describe('explicit screenshot secrecy')</c> (<c>tests/unit/fixture-runtime.test.ts</c>). Not ported, because the port has
/// no such feature: the <c>provider</c> case of "captures before and after a %s secret fill", "still captures when a credential provider
/// fails before supplying a secret", and "refuses a provider value under 6 code points ... at fill time with the unavailable code, pixels
/// untainted" (they need <c>config.credentials</c> and its provider functions, which the port does not have).
/// </summary>
public sealed class ExplicitScreenshotSecrecyTests : IDisposable
{
    private const string Sentinel = "synthetic-screenshot-secret-2718";

    private readonly string _results = Path.Combine(Path.GetTempPath(), "e2e-screenshot-secrecy-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_results))
        {
            Directory.Delete(_results, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Partial: only the <c>static</c> source, a <see cref="Secret"/> the test holds, since the port has no credential providers; and the port has
    /// no <c>setSecretRegistry</c>, so the session is told the secret with <c>Remember</c>. The artifact is read from the step's record and
    /// from the report it ends in, where upstream reads the arguments of <c>register</c>.
    /// </summary>
    [Fact]
    public async Task Partial_Captures_before_and_after_a_static_secret_fill_with_the_secret_kept_out_of_the_steps()
    {
        var engine = new ScreenshotArtifactEngine("screenshots/evidence.png");
        var secret = Secret.Create("member-password", Sentinel);
        var session = await StartAsync(engine);
        try
        {
            session.Remember([secret]);
            var expected = Path.Combine(AttemptDir(), "screenshots", "evidence.png");
            Assert.Equal(expected, await session.App.ScreenshotAsync("before-fill"));
            Assert.Equal(["before-fill"], engine.Labels);
            Assert.Single(session.Steps[^1].Artifacts);
            engine.Labels.Clear();

            await session.Screen.GetByRole("textbox").FillAsync(secret);
            Assert.Equal(Sentinel, engine.Filled);
            Assert.Equal(expected, await session.App.ScreenshotAsync("after-fill"));
            Assert.Equal(["after-fill"], engine.Labels);
            Assert.Single(session.Steps[^1].Artifacts);
            var last = session.Steps[^1];
            Assert.Equal("app.screenshot", last.Api);
            Assert.Equal(StepStatus.Passed, last.Status);
            Assert.DoesNotContain(Sentinel, JsonSerializer.Serialize(session.Steps.Select(step => new { step.Api, step.Label, step.Explanation, step.Error })), StringComparison.Ordinal);

            session.Complete();
            await session.DisposeAsync();
            var artifacts = session.Report()!.Run.Results[0].Attempts[0].Artifacts;
            Assert.Equal(2, artifacts.Count);
            Assert.All(artifacts, artifact =>
            {
                Assert.Equal(E2E.Reports.ArtifactKind.Screenshot, artifact.Kind);
                Assert.EndsWith("screenshots/evidence.png", artifact.Path!.Replace('\\', '/'), StringComparison.Ordinal);
            });
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    /// <summary>
    /// .NET-only: no upstream <c>it</c> puts a secret in a screenshot label. The step record redacts its label, as upstream's
    /// <c>StepRecorder.run</c> does. The port also redacts the label before the engine names the file (upstream passes it as given),
    /// so a secret never reaches the file name or the artifact path.
    /// </summary>
    [Fact]
    public async Task Keeps_a_secret_spelled_into_the_label_out_of_the_step_and_the_file_name()
    {
        var engine = new ScreenshotArtifactEngine("screenshots/evidence.png");
        var session = await StartAsync(engine);
        try
        {
            session.Remember([Secret.Create("member-password", Sentinel)]);
            await session.App.ScreenshotAsync("after-" + Sentinel);

            var step = session.Steps[^1];
            Assert.Equal("app.screenshot", step.Api);
            Assert.DoesNotContain(Sentinel, step.Label, StringComparison.Ordinal);
            Assert.DoesNotContain(Sentinel, Assert.Single(engine.Labels)!, StringComparison.Ordinal);
            session.Complete();
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    private string AttemptDir() => Path.Combine(_results, AttemptReport.ResultSegmentOf(null, "explicit screenshot"), "attempt-1");

    private Task<E2ESession> StartAsync(ScreenshotArtifactEngine engine) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            TestTitle = "explicit screenshot",
            ResultsDirectory = _results,
            CacheEnabled = false,
        });
}
