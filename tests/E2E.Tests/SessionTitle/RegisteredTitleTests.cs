// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.SessionTitle;

// .NET-only: upstream checks a title where its registry accepts it (collect/registry.ts, title()), and the port's registration is test
// discovery, so the same checks run there (HostTestFilters.RegisteredTitle). No upstream it covers the port's placement.
public sealed class RegisteredTitleTests
{
    [Fact]
    public void Rejects_a_title_over_512_UTF_8_bytes()
    {
        var error = Assert.Throws<ArgumentException>(() => HostTestFilters.RegisteredTitle(new string('a', 513), []));
        Assert.Contains("512 UTF-8 bytes", error.Message);
    }

    [Fact]
    public void Rejects_a_title_that_passes_as_written_but_not_once_its_secrets_are_redacted()
    {
        // 200 bytes as written; each "ab" becomes the 11-byte marker "<secret:pw>", so the redacted title is 1100 bytes.
        var title = string.Concat(Enumerable.Repeat("ab", 100));
        var error = Assert.Throws<ArgumentException>(() => HostTestFilters.RegisteredTitle(title, [("pw", "ab")]));
        Assert.Contains("once secret values are redacted", error.Message);
    }

    [Fact]
    public void Normalizes_a_title_to_NFC_once_it_is_registered()
    {
        Assert.Equal("café", HostTestFilters.RegisteredTitle("café", []));
    }
}
