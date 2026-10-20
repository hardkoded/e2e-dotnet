// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.FailureEvidence;

namespace E2E.Tests.ScreenTreeSecrets;

/// <summary>
/// Upstream's <c>describe('screen.tree')</c> (<c>tests/integration/screen-tree-secrets.test.ts</c>): a secret the screen shows reads as its
/// placeholder, as it does in the trace's screen at failure. Driven through a session over a fake engine, with no runner.
/// </summary>
public sealed class ScreenTreeTests
{
    private const string Token = "tree-token-42";

    [Fact]
    public async Task Redacts_the_secrets_the_run_knows()
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new EvidenceEngine("Token " + Token),
            TestTitle = "reads the tree",
            BaseUrl = "http://127.0.0.1:4599",
            CacheEnabled = false,
        });
        session.Remember([Secret.Create("apiToken", Token)]);
        await session.App.OpenAsync("/");

        var tree = await session.Screen.TreeAsync();

        Assert.DoesNotContain(Token, tree, StringComparison.Ordinal);
        Assert.Contains("Token <secret:apiToken>", tree, StringComparison.Ordinal);
    }
}
