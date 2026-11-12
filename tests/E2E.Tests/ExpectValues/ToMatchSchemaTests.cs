// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ExpectValues;

/// <summary>
/// <c>describe('toMatchSchema')</c> of upstream's <c>expect-values.test.ts</c>. There is no Standard Schema in .NET, so the schemas are
/// <see cref="StandardSchemaV1{TOutput}"/> delegates written here by hand: <c>User</c> stands for <c>z.object({ id, email, role })</c> and
/// <c>UserList</c> for <c>z.array(User)</c>. Partial (<c>Partial_</c>): the refusal of a value that is no Standard Schema is a null schema
/// (<c>INVALID_ARGUMENT</c>, with a message that names a delegate, not Standard Schema v1), and a schema that validates asynchronously cannot be passed, because the delegate is synchronous.
/// </summary>
public sealed class ToMatchSchemaTests
{
    /// <summary><c>z.object({ id: z.number(), email: z.email(), role: z.enum(['admin', 'member']).default('member') })</c>.</summary>
    private static readonly StandardSchemaV1<Dictionary<string, object?>> User = ValidateUser;

    /// <summary><c>z.array(User)</c>.</summary>
    private static readonly StandardSchemaV1<List<Dictionary<string, object?>>> UserList = ValidateUserList;

    [Fact]
    public void Returns_the_schema_output_for_a_matching_value_defaults_applied()
    {
        var user = Expect.That<object?>(new Dictionary<string, object?> { ["id"] = 1, ["email"] = "ada@example.com" }).ToMatchSchema(User);
        Expect.That(user).ToEqual(new Dictionary<string, object?> { ["id"] = 1, ["email"] = "ada@example.com", ["role"] = "member" });
        Expect.That(Expect.That<object?>(new List<object?> { new Dictionary<string, object?> { ["id"] = 2, ["email"] = "bo@example.com", ["role"] = "admin" } }).ToMatchSchema(UserList)).ToHaveLength(1);
    }

    [Fact]
    public void Fails_listing_every_issue_by_its_path()
    {
        FailsWith(
            () => Expect.That<object?>(new List<object?>
            {
                new Dictionary<string, object?> { ["id"] = 1, ["email"] = "ada@example.com" },
                new Dictionary<string, object?> { ["id"] = "2", ["email"] = "nope" },
            }).ToMatchSchema(UserList),
            @"to match the schema:\n- 1\.id: .+\n- 1\.email: ");
        FailsWith(() => Expect.That<object?>(null, "GET /users").ToMatchSchema(User), @"^GET /users: expected null to match the schema:\n- ");
    }

    [Fact]
    public void Negates_returning_nothing()
    {
        Expect.That(Expect.That<object?>(new Dictionary<string, object?> { ["id"] = "x" }).Not.ToMatchSchema(User)).ToBeNull();
        FailsWith(
            () => Expect.That<object?>(new Dictionary<string, object?> { ["id"] = 1, ["email"] = "ada@example.com" }).Not.ToMatchSchema(User),
            "not to match the schema");
    }

    [Fact]
    public void Partial_refuses_a_value_that_is_no_Standard_Schema_and_a_schema_that_validates_asynchronously()
    {
        var error = Assert.Throws<TestException>(() => Expect.That<object?>(new object()).ToMatchSchema<object>(null!));
        Assert.Equal("INVALID_ARGUMENT", error.Code);
    }

    /// <summary>Validates the way <c>User</c> does: every field is checked, and <c>role</c> defaults to <c>member</c>.</summary>
    private static StandardSchemaV1Result<Dictionary<string, object?>> ValidateUser(object? value)
    {
        if (value is not Dictionary<string, object?> record)
        {
            return StandardSchemaV1Result<Dictionary<string, object?>>.Failure([new StandardSchemaV1Issue("expected an object")]);
        }

        var issues = new List<StandardSchemaV1Issue>();
        if (record.GetValueOrDefault("id") is not int)
        {
            issues.Add(new StandardSchemaV1Issue("expected a number", ["id"]));
        }

        if (record.GetValueOrDefault("email") is not string email || !email.Contains('@'))
        {
            issues.Add(new StandardSchemaV1Issue("Invalid email", ["email"]));
        }

        var role = record.GetValueOrDefault("role") ?? "member";
        if (role is not ("admin" or "member"))
        {
            issues.Add(new StandardSchemaV1Issue("expected admin or member", ["role"]));
        }

        return issues.Count > 0
            ? StandardSchemaV1Result<Dictionary<string, object?>>.Failure(issues)
            : StandardSchemaV1Result<Dictionary<string, object?>>.Success(new Dictionary<string, object?>
            {
                ["id"] = record["id"],
                ["email"] = record["email"],
                ["role"] = role,
            });
    }

    /// <summary>Validates the way <c>z.array(User)</c> does: every element is checked, and each issue is prefixed with its index.</summary>
    private static StandardSchemaV1Result<List<Dictionary<string, object?>>> ValidateUserList(object? value)
    {
        if (value is not List<object?> items)
        {
            return StandardSchemaV1Result<List<Dictionary<string, object?>>>.Failure([new StandardSchemaV1Issue("expected an array")]);
        }

        var outputs = new List<Dictionary<string, object?>>();
        var issues = new List<StandardSchemaV1Issue>();
        for (var index = 0; index < items.Count; index++)
        {
            var result = ValidateUser(items[index]);
            if (result.Issues is null)
            {
                outputs.Add(result.Value!);
                continue;
            }

            issues.AddRange(result.Issues.Select(issue => issue with { Path = [index, .. issue.Path ?? []] }));
        }

        return issues.Count > 0
            ? StandardSchemaV1Result<List<Dictionary<string, object?>>>.Failure(issues)
            : StandardSchemaV1Result<List<Dictionary<string, object?>>>.Success(outputs);
    }

    private static void FailsWith(Action action, string pattern)
    {
        var error = Assert.Throws<TestException>(action);
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Matches(pattern, error.Message);
    }
}
