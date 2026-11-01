// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.ScreenshotAssertions;

namespace E2E.Tests.ExpectSoft;

/// <summary>
/// .NET-only: upstream types <c>expect.soft(screen)</c> in <c>tests/types/sdk-types.ts</c> and has no <c>it</c> for it, since
/// <c>soften</c> wraps any expectation. The port has one class per expectation, so this checks that <c>Expect.Soft(screen)</c> keeps an
/// <c>ASSERTION_FAILED</c> of the screenshot matcher as the other soft matchers do.
/// </summary>
public sealed class SoftScreenTests
{
    [Fact]
    public async Task Expect_Soft_screen_keeps_a_screenshot_failure_and_lets_the_caller_continue()
    {
        using var project = new ScreenProject(new ScreenEngine());
        await using var session = await StartAsync(project);

        // The first run has no stored screenshot, so the matcher writes one and fails.
        await Expect.Soft(session.Screen).ToHaveScreenshotAsync("home");
        await Expect.Soft(session.Screen).Not.ToHaveScreenshotAsync("home", new ScreenshotOptions { Timeout = TimeSpan.FromSeconds(1) });

        var error = Assert.IsType<TestException>(session.CloseSoftFailures());
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.StartsWith("2 soft assertions failed\n1. ", error.Message, StringComparison.Ordinal);
        Assert.Contains("no stored screenshot", error.Message, StringComparison.Ordinal);
    }

    private static Task<E2ESession> StartAsync(ScreenProject project) => E2ESession.StartAsync(new E2ESessionOptions
    {
        Engine = project.Engine,
        TestTitle = "soft screen",
        ProjectRoot = project.Directory,
        TargetName = "fake",
        Trace = new ResolvedRecording(RecordingMode.Off, RecordingSource.Target),
        AssertionTimeout = TimeSpan.FromSeconds(5),
        SnapshotDirectoryOf = _ => project.Snapshots("soft"),
    });
}
