// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ExpectSoft;

public sealed class ExpectSoftTests
{
    [Fact]
    public async Task Throws_once_the_collection_is_closed_as_an_afterEach_hook_finds_it()
    {
        await using var session = await StartAsync();
        await session.App.OpenAsync("/settings/billing");
        Assert.Null(session.CloseSoftFailures());
        await Assert.ThrowsAsync<TestException>(() => Expect.Soft(session.Screen.GetByRole("status", "Pro")).ToBeVisibleAsync());
    }

    private static Task<E2ESession> StartAsync()
    {
        return E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(BillingWorld.Create()),
            BaseUrl = "https://billing.test",
            CacheEnabled = false,
            TestTitle = "billing > soft",
            AssertionTimeout = TimeSpan.FromMilliseconds(100),
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            TestTimeout = TimeSpan.FromSeconds(10),
        });
    }
}
