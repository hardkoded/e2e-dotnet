// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.ExpectPoll;

/// <summary>
/// Ported from upstream's <c>expect-poll.test.ts</c>. Only the test that needs an NUnit constraint is here. The rest are in
/// <c>E2E.Tests.ExpectPoll.ExpectPollTests</c>.
/// </summary>
public sealed class ExpectPollTests
{
    [Test]
    public async Task Treats_a_matcher_type_complaint_as_not_yet_like_any_other_failing_sample()
    {
        var reads = 0;
        object Read() => ++reads > 1 ? "hello world" : 42;

        await Expect.Poll(Read, new PollOptions { Interval = TimeSpan.FromMilliseconds(5) }).ToMatchAsync(Does.Contain("world"));

        Assert.That(reads, Is.EqualTo(2));
    }
}
