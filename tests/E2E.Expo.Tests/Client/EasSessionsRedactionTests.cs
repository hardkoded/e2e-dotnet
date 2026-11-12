// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Expo.Tests.Client;

/// <summary>.NET-only, no upstream <c>it</c>: upstream redacts at the report boundary; the port's client hides its own token in the error text it builds from Expo's answer.</summary>
public sealed class EasSessionsRedactionTests
{
    [Fact]
    public async Task HidesTheTokenInAnErrorExpoAnswersWith()
    {
        var graphql = await Assert.ThrowsAsync<EasException>(
            () => EasSessionsTests.Answering("""{ "errors": [{ "message": "bad token expo-test" }] }""").StateAsync("s1", CancellationToken.None));
        Assert.Equal("Expo: bad token *****", graphql.Message);
        var page = await Assert.ThrowsAsync<InvalidOperationException>(() => EasSessionsTests.Answering("Forbidden: expo-test", 403).StateAsync("s1", CancellationToken.None));
        Assert.Equal("Expo: HTTP 403, Forbidden: *****", page.Message);
    }
}
