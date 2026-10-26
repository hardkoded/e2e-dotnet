// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.DialogsFrames;

/// <summary>
/// <c>test.describe('downloads')</c> of upstream's <c>apps/testbed/tests/dialogs-frames.e2e.ts</c>, against the playground's
/// <c>/downloads</c> page.
/// </summary>
public sealed class DownloadsTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Waits_for_a_triggered_download()
    {
        await App.OpenAsync("/downloads");
        var download = await Browser.WaitForDownloadAsync(() => Screen.GetByRole("link", "Download report").TapAsync());
        Assert.That(download.SuggestedFilename, Is.EqualTo("report.csv"));
        Assert.That(download.Path, Does.Contain("downloads/"));
    }
}
