// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The <c>expect(() =&gt; validateDownloadUrl(url)).toThrow(DownloadError)</c> and <c>.not.toThrow()</c> assertions of <c>validate-download-url.test.ts</c>.</summary>
internal static class ValidateDownloadUrlAssertions
{
    internal static void AssertAllowed(string url) => Sdk.ValidateDownloadUrl.Validate(url);

    internal static void AssertBlocked(string url) =>
        Assert.Throws<DownloadException>(() => Sdk.ValidateDownloadUrl.Validate(url));
}
