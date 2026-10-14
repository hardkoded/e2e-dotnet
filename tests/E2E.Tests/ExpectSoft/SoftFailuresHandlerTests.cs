// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ExpectSoft;

/// <summary>
/// No upstream <c>it</c>: the failure handler is how the .NET test adapters report soft failures to their framework.
/// </summary>
public sealed class SoftFailuresHandlerTests
{
    [Fact]
    public void A_failure_sent_to_the_handler_still_counts_as_kept()
    {
        var handled = new List<TestException>();
        var failures = new SoftFailures(handled.Add);
        Assert.False(failures.Any);

        Assert.True(failures.Keep(new TestException("ASSERTION_FAILED", "nope")));

        Assert.True(failures.Any);
        Assert.Single(handled);
        Assert.Null(failures.Close());
        Assert.True(failures.Any);
    }
}
