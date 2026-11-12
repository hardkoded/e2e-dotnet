// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ExpectSoft;

/// <summary>
/// <c>describe('expect.soft toMatchSchema')</c> of upstream's <c>expect-soft.test.ts</c>. Partial (<c>Partial_</c>): upstream reads the
/// failure from a live attempt's <c>soft.close()</c>, and the port keeps it on a <see cref="SoftFailures"/> of its own, with the
/// schema a hand-written <see cref="StandardSchemaV1{TOutput}"/> standing in for <c>z.number()</c>.
/// </summary>
public sealed class ExpectSoftToMatchSchemaTests
{
    private static readonly StandardSchemaV1<int> Number = new(Validate);

    [Fact]
    public void Partial_returns_the_output_on_a_pass_and_undefined_after_a_kept_failure()
    {
        var failures = new SoftFailures();
        var one = new SoftValueExpectation<object?>(new ValueExpectation<object?>(1, false, null), failures);
        var text = new SoftValueExpectation<object?>(new ValueExpectation<object?>("1", false, null), failures);

        Assert.Equal(1, one.ToMatchSchema(Number));
        // A value-type output has no null: the kept failure returns default (0) where upstream returns undefined.
        Assert.Equal(default, text.ToMatchSchema(Number));

        var error = failures.Close();
        Assert.NotNull(error);
        Assert.Matches(@"^1 soft assertion failed\n1\. expected ""1"" to match the schema:", error.Message);
    }

    private static StandardSchemaV1Result<int> Validate(object? value) => value is int number
        ? StandardSchemaV1Result<int>.Success(number)
        : StandardSchemaV1Result<int>.Failure([new StandardSchemaV1Issue("expected a number")]);
}
