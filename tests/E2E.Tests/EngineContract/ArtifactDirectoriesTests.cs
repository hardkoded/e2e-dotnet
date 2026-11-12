// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using E2E.Engine;

namespace E2E.Tests.EngineContract;

/// <summary>
/// <c>describe('artifact directories')</c> of upstream <c>engine-contract.test.ts</c>. Upstream runs a project through the runner over a
/// fake engine and reads the report and the results directory. The port has no runner, so the test runs the two failing tests as two
/// sessions over a fake <see cref="IEngine"/>, which write under one results directory, and reads each session's report.
/// </summary>
public sealed class ArtifactDirectoriesTests : IDisposable
{
    /// <summary>A describe long enough that the two tests below share every word their artifact directories keep.</summary>
    private const string LongDescribe = "a returning customer with a saved card and an expired coupon on file who reloads the checkout page twice before paying";

    // The output directory holds the results and the run's report.json beside them.
    private readonly string _results = Path.Combine(Directory.CreateTempSubdirectory("e2e-artifact-directories-").FullName, "results");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_results)!, recursive: true);

    [Fact]
    public async Task Two_failing_tests_whose_titles_share_their_first_words_keep_their_evidence_apart_on_disk_and_in_the_report_PARTIAL_the_title_is_one_string_and_a_session_stands_for_the_run()
    {
        var first = await FailAsync(LongDescribe + " > keeps the coupon after a reload", "first", shade: 40);
        var second = await FailAsync(LongDescribe + " > drops the coupon after sign-out", "second", shade: 200);
        Assert.Equal(first.TestId[..120], second.TestId[..120]);
        Assert.Equal([Reports.ResultStatus.Failed, Reports.ResultStatus.Failed], [first.Status, second.Status]);

        var a = ScreenshotOf(first);
        var b = ScreenshotOf(second);
        Assert.NotEqual(a.Path, b.Path);
        Assert.NotEqual(a.Sha256, b.Sha256);
        foreach (var artifact in new[] { a, b })
        {
            var absolute = Path.Combine(_results, artifact.Path!);
            Assert.True(File.Exists(absolute));
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(absolute))), artifact.Sha256);
        }

        // One directory per test, the same words and another id, not one written twice.
        var directories = Directory.GetDirectories(_results).Select(Path.GetFileName).ToList();
        Assert.Equal(2, directories.Count);
        Assert.Equal(["a-returning-customer-with-a-", "a-returning-customer-with-a-"], directories.Select(directory => directory![..^16]));
    }

    private static Reports.ArtifactRecord ScreenshotOf(Reports.ReportResult result) =>
        result.Attempts[0].Artifacts.First(artifact => artifact.Kind == Reports.ArtifactKind.Screenshot);

    /// <summary>Runs a test that opens the app and throws, over an engine whose screenshots are one shade, and returns its result.</summary>
    private async Task<Reports.ReportResult> FailAsync(string title, string message, byte shade)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new FakeEngine(new DocumentEngine(BillingWorld.Create("Submit")), shade),
            BaseUrl = "https://billing.test",
            TestTitle = title,
            ResultsDirectory = _results,
            ProjectRoot = _results,
        });
        await session.App.OpenAsync("/settings/billing");
        session.Complete(new TestException("TEST_FAILED", message));
        await session.DisposeAsync();
        return Assert.Single(session.Report()!.Run.Results);
    }
}
