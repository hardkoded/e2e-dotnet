// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Sdk;

/// <summary>Upstream's <c>FlexibleSchema</c> (<c>provider-utils/src/schema.ts</c>): a <see cref="Schema"/> or a <see cref="LazySchema"/>.</summary>
public abstract class FlexibleSchema
{
    private protected FlexibleSchema() { }
}

/// <summary>The outcome of <see cref="Schema.Validate"/>.</summary>
public sealed record ValidationResult
{
    /// <summary>Whether the value matched the schema.</summary>
    public required bool Success { get; init; }

    /// <summary>The value the schema parsed. Set when <see cref="Success"/> is true.</summary>
    public JsonNode? Value { get; init; }

    /// <summary>Why the value did not match. Set when <see cref="Success"/> is false.</summary>
    public string? Error { get; init; }
}

/// <summary>Upstream's <c>Schema</c>: a JSON schema and a validator for the value it describes.</summary>
public sealed class Schema : FlexibleSchema
{
    private readonly Lazy<JsonObject> _jsonSchema;

    internal Schema(Func<JsonObject> jsonSchema, Func<JsonNode?, ValidationResult> validate)
    {
        _jsonSchema = new Lazy<JsonObject>(jsonSchema);
        Validate = validate;
    }

    /// <summary>Upstream's <c>jsonSchema</c>.</summary>
    public JsonObject JsonSchema => _jsonSchema.Value;

    /// <summary>Upstream's <c>validate</c>.</summary>
    public Func<JsonNode?, ValidationResult> Validate { get; }
}

/// <summary>Upstream's <c>LazySchema</c>: a schema built the first time something reads it.</summary>
public sealed class LazySchema : FlexibleSchema
{
    private readonly Lazy<Schema> _schema;

    internal LazySchema(Func<Schema> createSchema) => _schema = new Lazy<Schema>(createSchema);

    internal Schema Resolve() => _schema.Value;
}

/// <summary>
/// The functions of <c>provider-utils/src/schema.ts</c> (<c>lazySchema</c>, <c>zodSchema</c>, <c>asSchema</c>) and
/// <c>validate-types.ts</c> (<c>safeValidateTypes</c>). C# has no free functions, so they live on this class. They are synchronous,
/// where upstream's <c>jsonSchema</c> and <c>safeValidateTypes</c> return promises.
/// </summary>
public static class SchemaUtils
{
    /// <summary>Upstream's <c>lazySchema</c>.</summary>
    public static LazySchema LazySchema(Func<Schema> createSchema) => new(createSchema);

    /// <summary>Upstream's <c>zodSchema</c>.</summary>
    public static Schema ZodSchema(ZodType zodType) => new(zodType.ToJsonSchema, value =>
        {
            var result = zodType.SafeParse(value);
            return new ValidationResult
            {
                Success = result.Success,
                Value = result.Data,
                Error = result.Error,
            };
        });

    /// <summary>Upstream's <c>asSchema</c>.</summary>
    public static Schema AsSchema(FlexibleSchema schema) =>
        schema switch
        {
            Schema s => s,
            LazySchema lazy => lazy.Resolve(),
            _ => throw new ArgumentException("Unknown schema type.", nameof(schema)),
        };

    /// <summary>Upstream's <c>safeValidateTypes</c>.</summary>
    public static ValidationResult SafeValidateTypes(JsonNode? value, FlexibleSchema schema) =>
        AsSchema(schema).Validate(value);
}
