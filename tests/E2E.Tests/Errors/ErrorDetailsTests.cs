// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Errors;

/// <summary>
/// Upstream's <c>describe('error details')</c> (<c>tests/unit/errors.test.ts</c>). Not ported: "keeps the details of an error classified
/// from another module copy, and drops a shape it does not know" (there is one copy of an assembly, and the type forbids a shape it does
/// not know).
/// </summary>
public sealed class ErrorDetailsTests
{
    [Fact]
    public void Carries_structured_details_onto_the_serialized_error_bounded_and_sanitized_and_drops_empty_ones()
    {
        var error = new TestException("ASSERTION_FAILED", "nope")
        {
            Details = new ErrorDetails { Expected = "a", Observed = "b\u0007 red", Name = "", Locator = new string('x', 5000), Matches = 3 },
        };

        var serialized = ErrorSerializer.Serialize(error);

        Assert.Equal(new ErrorDetails { Expected = "a", Observed = "b� red", Locator = new string('x', 2048), Matches = 3 }, serialized.Details);
        Assert.Null(ErrorSerializer.Serialize(new TestException("ASSERTION_FAILED", "nope") { Details = new ErrorDetails { Name = "" } }).Details);
        Assert.Null(ErrorSerializer.Serialize(new TestException("ASSERTION_FAILED", "nope")).Details);
    }

    // Partial: the port keeps no stack text, so only the message and the details are redacted.
    [Fact]
    public void Redacts_secret_values_from_the_message_and_the_details_when_given_a_redactor()
    {
        static string Redact(string text) => text.Replace("hunter2", "<secret:password>", StringComparison.Ordinal);
        var error = new TestException("ASSERTION_FAILED", "expected hunter2") { Details = new ErrorDetails { Observed = "value \"hunter2\"", Matches = 1 } };

        var serialized = ErrorSerializer.Serialize(error, redact: Redact);

        Assert.Equal("expected <secret:password>", serialized.Message);
        Assert.Equal(new ErrorDetails { Observed = "value \"<secret:password>\"", Matches = 1 }, serialized.Details);
    }
}
