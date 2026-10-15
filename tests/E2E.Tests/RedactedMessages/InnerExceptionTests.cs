// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RedactedMessages;

/// <summary>
/// No upstream <c>it</c>: upstream redacts the stack of a cause at its report boundary, which the port has not.
/// </summary>
public sealed class InnerExceptionTests
{
    private const string Secret = "hunter2-secret";

    private static string Redact(string text) => text.Replace(Secret, "<secret:password>", StringComparison.Ordinal);

    [Fact]
    public void Redacts_each_message_of_a_cause_chain_and_keeps_the_types_and_codes()
    {
        var cause = new EngineException("NOT_ACTIONABLE", "node " + Secret + " is covered", new InvalidOperationException("page said " + Secret));

        var failure = ErrorRedaction.Failure("ASSERTION_FAILED", "failed on " + Secret, cause, Redact);

        Assert.Equal("failed on <secret:password>", failure.Message);
        var engine = Assert.IsType<EngineException>(failure.InnerException);
        Assert.Equal("NOT_ACTIONABLE", engine.Code);
        Assert.Equal("node <secret:password> is covered", engine.Message);
        Assert.Equal("page said <secret:password>", engine.InnerException!.Message);
    }

    [Fact]
    public void Keeps_a_cause_that_holds_no_secret_as_it_was_thrown()
    {
        var cause = new InvalidOperationException("nothing to hide");

        var failure = ErrorRedaction.Failure("ASSERTION_FAILED", "failed", cause, Redact);

        Assert.Same(cause, failure.InnerException);
    }
}
