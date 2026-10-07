// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ExpectSoft;

public sealed class ExpectSoftTests
{
    [Fact]
    public async Task Throws_once_the_collection_is_closed_as_an_afterEach_hook_finds_it()
    {
        await using var session = await PollSoftTests.StartAsync();
        await session.App.OpenAsync("/settings/billing");
        Assert.Null(session.CloseSoftFailures());
        await Assert.ThrowsAsync<TestException>(() => Expect.Soft(session.Screen.GetByRole("status", "Pro")).ToBeVisibleAsync());
    }
}
