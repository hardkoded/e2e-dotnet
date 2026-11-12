// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Tests.FixtureRuntime;

/// <summary>
/// Upstream's <c>describe('explicit screenshot secrecy')</c> (<c>tests/unit/fixture-runtime.test.ts</c>). The credential's provider is set in
/// code by <see cref="E2EConfig.WithCredential"/>, and the session holds the config's accounts, as upstream's <c>setSecretRegistry(config)</c> does.
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
    /// Partial: the artifact is read from the step's record and from the report it ends in, where upstream reads the arguments of <c>register</c>.
    /// </summary>
    [Theory]
    [InlineData("static")]
    [InlineData("provider")]
    public async Task Partial_Captures_before_and_after_a_source_secret_fill_with_the_secret_kept_out_of_the_steps(string source)
    {
        var engine = new ScreenshotArtifactEngine("screenshots/evidence.png");
        var session = await StartAsync(engine, MemberConfig(source == "static" ? Sentinel : null, _ => Task.FromResult(Sentinel)));
        try
        {
            var expected = Path.Combine(AttemptDir(), "screenshots", "evidence.png");
            Assert.Equal(expected, await session.App.ScreenshotAsync("before-fill"));
            Assert.Equal(["before-fill"], engine.Labels);
            Assert.Single(session.StepRecords[^1].Artifacts);
            engine.Labels.Clear();

            await session.Screen.GetByRole("textbox").FillAsync(E2E.Credentials.User("member").Password);
            Assert.Equal(Sentinel, engine.Filled);
            Assert.Equal(expected, await session.App.ScreenshotAsync("after-fill"));
            Assert.Equal(["after-fill"], engine.Labels);
            Assert.Single(session.StepRecords[^1].Artifacts);
            var last = session.StepRecords[^1];
            Assert.Equal("app.screenshot", last.Api);
            Assert.Equal(StepStatus.Passed, last.Status);
            Assert.DoesNotContain(Sentinel, JsonSerializer.Serialize(session.StepRecords.Select(step => new { step.Api, step.Label, step.Explanation, step.Error })), StringComparison.Ordinal);

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

    [Fact]
    public async Task Still_captures_when_a_credential_provider_fails_before_supplying_a_secret()
    {
        var engine = new ScreenshotArtifactEngine("screenshots/evidence.png");
        var session = await StartAsync(engine, MemberConfig(null, _ => Task.FromResult(string.Empty)));
        try
        {
            var error = await Assert.ThrowsAsync<ConfigurationException>(() => session.Screen.GetByRole("textbox").FillAsync(E2E.Credentials.User("member").Password));
            Assert.Equal("AUTH_CREDENTIAL_UNAVAILABLE", error.Code);
            Assert.Null(engine.Filled);

            await session.App.ScreenshotAsync();
            Assert.Single(engine.Labels);
            Assert.Single(session.StepRecords[^1].Artifacts);
            session.Complete();
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("🔐🔐🔐")]
    public async Task Refuses_a_provider_value_under_6_code_points_at_fill_time_with_the_unavailable_code(string value)
    {
        var engine = new ScreenshotArtifactEngine("screenshots/evidence.png");
        var session = await StartAsync(engine, MemberConfig(null, _ => Task.FromResult(value)));
        try
        {
            var error = await Assert.ThrowsAsync<ConfigurationException>(() => session.Screen.GetByRole("textbox").FillAsync(E2E.Credentials.User("member").Password));
            Assert.Equal("AUTH_CREDENTIAL_UNAVAILABLE", error.Code);
            Assert.Contains("at least 6 characters (code points)", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(value, error.Message, StringComparison.Ordinal);
            Assert.Null(engine.Filled);

            await session.App.ScreenshotAsync();
            Assert.Single(engine.Labels);
            session.Complete();
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

            var step = session.StepRecords[^1];
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

    /// <summary>A config whose account <c>member</c> has a static <paramref name="password"/>, or, with none, the password <paramref name="provider"/> computes.</summary>
    private static E2EConfig MemberConfig(string? password, SecretProvider provider)
    {
        var config = E2EConfig.Parse("{}", Path.GetTempPath(), _ => null);
        return password is null
            ? config.WithCredential("member", "ada", provider)
            : E2EConfig.Parse($$"""{ "credentials": { "member": { "username": "ada", "password": "{{password}}" } } }""", Path.GetTempPath(), _ => null);
    }

    private Task<E2ESession> StartAsync(ScreenshotArtifactEngine engine, E2EConfig? config = null) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Credentials = config?.Credentials,
            Engine = engine,
            TestTitle = "explicit screenshot",
            ResultsDirectory = _results,
            CacheEnabled = false,
        });
}
