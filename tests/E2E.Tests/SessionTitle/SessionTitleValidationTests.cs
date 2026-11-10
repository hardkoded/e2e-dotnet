// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;

namespace E2E.Tests.SessionTitle;

// .NET-only: upstream checks a title where its registry accepts it (collect/registry.ts, title()), and the port has no registry. A session
// is where the port takes a title, so the same two checks run at launch: the raw title, then the title once the run's secrets are redacted.
public sealed class SessionTitleValidationTests
{
    [Fact]
    public async Task Rejects_a_title_over_512_UTF_8_bytes()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => StartWith(new string('a', 513)));
        Assert.Contains("512 UTF-8 bytes", error.Message);
    }

    [Fact]
    public async Task Rejects_a_title_that_passes_as_written_but_not_once_its_secrets_are_redacted()
    {
        // 200 bytes as written; each "ab" becomes the 11-byte marker "<secret:pw>", so the redacted title is 1100 bytes.
        var title = string.Concat(Enumerable.Repeat("ab", 100));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => StartWith(title, [("pw", "ab")]));
        Assert.Contains("once secret values are redacted", error.Message);
    }

    private static Task<E2ESession> StartWith(string title, (string Name, string Value)[]? secrets = null) => E2ESession.StartAsync(new E2ESessionOptions
    {
        Engine = new DocumentEngine(CounterPage.World()),
        TestTitle = title,
        Config = secrets is null ? null : new ResolvedConfig { ProjectRoot = ".", Targets = [], AllSecrets = secrets },
    });
}
