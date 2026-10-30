// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.FixtureRuntime;

namespace E2E.Tests.OpenSession;

/// <summary>
/// Upstream's <c>describe('host.openSession')</c> (<c>tests/integration/open-session.test.ts</c>). The port's session is
/// <see cref="E2ESession"/>, upstream's per-test session. Not ported, because the port has no such feature: "cancels a fixture call still
/// running when the session closes", "returns cleanup failures from close and throws them from session disposal", "opens a config value,
/// resolved against cwd, with AsyncDisposable sessions", and "needs a target when the config declares several, and opens the one named" (they open
/// a hosted session through the built runner's <c>openHost</c> and a config file, which the port's host leaves to its caller).
/// </summary>
public sealed class HostOpenSessionTests : IDisposable
{
    private readonly string _results = Path.Combine(Path.GetTempPath(), "e2e-open-session-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_results))
        {
            Directory.Delete(_results, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Partial: only the <c>app.screenshot</c> lines of the <c>it</c>. The port has no hosted session with fixtures, step streaming, the
    /// trace file, or engine counters, so the target, <c>gadget</c>, step progress, and the disposal counts are not checked.
    /// </summary>
    [Fact]
    public async Task Partial_Opens_the_target_with_its_fixtures_streams_steps_and_closes_each_owner_once()
    {
        var engine = new ScreenshotArtifactEngine("screenshots/fake.png");
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            TestTitle = "home screen",
            ResultsDirectory = _results,
            CacheEnabled = false,
        });
        try
        {
            var artifactsDir = Path.Combine(_results, AttemptReport.ResultSegmentOf(null, "home screen"), "attempt-1");
            var shotPath = await session.App.ScreenshotAsync("home");
            Assert.Equal(Path.Combine(artifactsDir, "screenshots", "fake.png"), shotPath);
            Assert.True(File.Exists(shotPath));
            var shot = Assert.Single(session.Steps, step => step.Api == "app.screenshot");
            Assert.Equal(StepStatus.Passed, shot.Status);
            Assert.Single(shot.Artifacts);
        }
        finally
        {
            session.Complete();
            await session.DisposeAsync();
        }
    }
}
