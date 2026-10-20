// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RedactedMessages;

/// <summary>
/// No upstream <c>it</c>: upstream redacts a <c>toHaveScreenshot</c> failure at its report boundary, which the port has not.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ScreenshotMessageTests
{
    [Fact]
    public async Task Redacts_a_secret_the_locator_names_in_the_failure_after_a_secret_is_known()
    {
        using var site = await TinySite.StartAsync(RedactionSite.Page);
        var snapshots = Path.Combine(Path.GetTempPath(), "e2e-screenshot-message-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var session = await RedactionSite.StartAsync(site, "screenshot message", _ => snapshots);
            await session.App.OpenAsync("/");
            RedactionSite.Know(session);

            var failure = await Assert.ThrowsAsync<TestException>(() =>
                Expect.That(session.Screen.GetByText(RedactionSite.Token, exact: false)).ToHaveScreenshotAsync("token"));

            Assert.Equal("ASSERTION_FAILED", failure.Code);
            Assert.Contains("<secret:token>", failure.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(RedactionSite.Token, failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(snapshots))
            {
                Directory.Delete(snapshots, recursive: true);
            }
        }
    }
}
