// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Errors;

/// <summary>
/// Upstream's <c>describe('redacting an error a program catches itself')</c> (<c>tests/unit/errors.test.ts</c>). Not ported: 'drops details of a
/// shape it cannot read, which it cannot redact either' (the type forbids a shape it does not know, and an error with no details has none to
/// drop). Partial: the port's serialized error keeps no stack, so the third <c>it</c> checks the message and the details.
/// </summary>
public sealed class RedactingAnErrorAProgramCatchesItselfTests
{
    private static string Redact(string text) =>
        text.Replace("hunter2-secret", "<secret:pw>", StringComparison.Ordinal);

    [Fact]
    public void Rewrites_the_text_fields_of_its_details_and_keeps_the_counts()
    {
        var error = new TestException("LOCATOR_NOT_FOUND", "nothing matched")
        {
            Details = new ErrorDetails
            {
                Locator = "getByText(\"hunter2-secret\")",
                Name = "hunter2-secret",
                Matches = 0,
            },
        };

        Assert.Equal(
            new ErrorDetails
            {
                Locator = "getByText(\"<secret:pw>\")",
                Name = "<secret:pw>",
                Matches = 0,
            },
            ErrorSerializer.RedactErrorDetails(error.Details, Redact)
        );
    }

    // .NET-only: the port rebuilds the cause where upstream rewrites its details in place.
    [Fact]
    public void Rebuilds_a_cause_whose_details_alone_carry_the_value()
    {
        var error = new TestException("ASSERTION_FAILED", "expected a value")
        {
            Details = new ErrorDetails { Observed = "hunter2-secret" },
        };

        var safe = Assert.IsType<TestException>(ErrorRedaction.Cause(error, Redact));

        Assert.Equal("<secret:pw>", safe.Details!.Observed);
    }

    [Fact]
    public void Partial_Rewrites_the_message_and_details_of_a_serialized_error_taken_from_another_sessions_record()
    {
        var serialized = ErrorRedaction.RedactSerializedError(
            new SerializedError(
                "test",
                "ASSERTION_FAILED",
                "saw hunter2-secret",
                false,
                null,
                new ErrorDetails { Observed = "hunter2-secret", Matches = 1 }
            ),
            Redact
        );

        Assert.Equal(
            new SerializedError(
                "test",
                "ASSERTION_FAILED",
                "saw <secret:pw>",
                false,
                null,
                new ErrorDetails { Observed = "<secret:pw>", Matches = 1 }
            ),
            serialized
        );
    }
}
