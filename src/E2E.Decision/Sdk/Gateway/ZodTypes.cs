// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The outcome of <see cref="ZodType.SafeParse"/> (zod's <c>safeParse</c> result).</summary>
public sealed record ZodSafeParseResult
{
    /// <summary>Whether the value matched.</summary>
    public required bool Success { get; init; }

    /// <summary>The parsed value (zod's <c>data</c>). Objects without a catchall lose the keys the schema does not name.</summary>
    public JsonNode? Data { get; init; }

    /// <summary>The first mismatch, as <c>path: reason</c>.</summary>
    public string? Error { get; init; }
}

internal readonly record struct ZodParse(bool Success, JsonNode? Value, bool Omit, string? Error)
{
    public static ZodParse Ok(JsonNode? value) => new(true, value, false, null);

    public static ZodParse Omitted() => new(true, null, true, null);

    public static ZodParse Fail(string path, string reason) => new(false, null, false, path + ": " + reason);
}

/// <summary>
/// .NET-only: a type of the <c>z</c> object of <c>zod.ts</c> upstream (zod/v4/mini), as far as the Gateway tool schemas use it. It parses
/// a JSON value like zod does and writes the JSON schema <c>zodSchema</c> derives from it.
/// </summary>
public abstract class ZodType
{
    private protected ZodType() { }

    internal string? DescriptionText { get; private set; }

    internal virtual bool IsOptional => false;

    /// <summary>Upstream's <c>safeParse</c>.</summary>
    public ZodSafeParseResult SafeParse(JsonNode? value)
    {
        var result = Parse(value, true, "$");
        return new ZodSafeParseResult
        {
            Success = result.Success,
            Data = result.Success ? result.Value : null,
            Error = result.Error,
        };
    }

    internal static ZodParse Missing(string path) => ZodParse.Fail(path, "required");

    internal abstract ZodParse Parse(JsonNode? value, bool present, string path);

    internal abstract JsonObject BuildJsonSchema();

    internal JsonObject ToJsonSchema()
    {
        var schema = BuildJsonSchema();
        if (DescriptionText is not null)
        {
            schema["description"] = DescriptionText;
        }

        return schema;
    }

    /// <summary>The <c>describe</c> of <c>zod.ts</c>: a copy of the schema with a description.</summary>
    internal ZodType Describe(string description)
    {
        var clone = (ZodType)MemberwiseClone();
        clone.DescriptionText = description;
        return clone;
    }
}

internal sealed class ZodString(int? minLength, int? maxLength, bool url) : ZodType
{
    internal override ZodParse Parse(JsonNode? value, bool present, string path)
    {
        if (!present)
        {
            return Missing(path);
        }

        if (value is not JsonValue v || !v.TryGetValue<string>(out var text))
        {
            return ZodParse.Fail(path, "expected string");
        }

        if (minLength is { } min && text.Length < min)
        {
            return ZodParse.Fail(path, "too small");
        }

        if (maxLength is { } max && text.Length > max)
        {
            return ZodParse.Fail(path, "too big");
        }

        if (url && !Uri.TryCreate(text, UriKind.Absolute, out _))
        {
            return ZodParse.Fail(path, "invalid URL");
        }

        return ZodParse.Ok(JsonValue.Create(text));
    }

    internal override JsonObject BuildJsonSchema()
    {
        var schema = new JsonObject { ["type"] = "string" };
        if (minLength is { } min)
        {
            schema["minLength"] = min;
        }

        if (maxLength is { } max)
        {
            schema["maxLength"] = max;
        }

        if (url)
        {
            schema["format"] = "uri";
        }

        return schema;
    }
}

internal sealed class ZodNumber(bool integer, double? minimum, double? maximum) : ZodType
{
    internal override ZodParse Parse(JsonNode? value, bool present, string path)
    {
        if (!present)
        {
            return Missing(path);
        }

        if (
            value is not JsonValue v
            || v.GetValueKind() != JsonValueKind.Number
            || !double.TryParse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            || !double.IsFinite(number)
        )
        {
            return ZodParse.Fail(path, "expected number");
        }

        if (integer && number != Math.Floor(number))
        {
            return ZodParse.Fail(path, "expected int");
        }

        if (minimum is { } min && number < min)
        {
            return ZodParse.Fail(path, "too small");
        }

        if (maximum is { } max && number > max)
        {
            return ZodParse.Fail(path, "too big");
        }

        return ZodParse.Ok(value.DeepClone());
    }

    internal override JsonObject BuildJsonSchema()
    {
        var schema = new JsonObject { ["type"] = integer ? "integer" : "number" };
        if (minimum is { } min)
        {
            schema["minimum"] = min;
        }

        if (maximum is { } max)
        {
            schema["maximum"] = max;
        }

        return schema;
    }
}

internal sealed class ZodBoolean : ZodType
{
    internal override ZodParse Parse(JsonNode? value, bool present, string path) =>
        !present ? Missing(path)
        : value is JsonValue v && v.TryGetValue<bool>(out _) ? ZodParse.Ok(value.DeepClone())
        : ZodParse.Fail(path, "expected boolean");

    internal override JsonObject BuildJsonSchema() => new() { ["type"] = "boolean" };
}

internal sealed class ZodEnum(IReadOnlyList<string> values) : ZodType
{
    internal override ZodParse Parse(JsonNode? value, bool present, string path) =>
        !present ? Missing(path)
        : value is JsonValue v && v.TryGetValue<string>(out var text) && values.Contains(text)
            ? ZodParse.Ok(value.DeepClone())
        : ZodParse.Fail(path, "invalid option");

    internal override JsonObject BuildJsonSchema()
    {
        var options = new JsonArray();
        foreach (var option in values)
        {
            options.Add(option);
        }

        return new JsonObject { ["type"] = "string", ["enum"] = options };
    }
}

internal sealed class ZodArray(ZodType item) : ZodType
{
    internal override ZodParse Parse(JsonNode? value, bool present, string path)
    {
        if (!present)
        {
            return Missing(path);
        }

        if (value is not JsonArray array)
        {
            return ZodParse.Fail(path, "expected array");
        }

        var result = new JsonArray();
        for (var i = 0; i < array.Count; i++)
        {
            var parsed = item.Parse(array[i], true, path + "[" + i + "]");
            if (!parsed.Success)
            {
                return parsed;
            }

            result.Add(parsed.Value);
        }

        return ZodParse.Ok(result);
    }

    internal override JsonObject BuildJsonSchema() => new() { ["type"] = "array", ["items"] = item.ToJsonSchema() };
}

/// <summary><c>z.object</c>, and <c>z.catchall</c> over it when <paramref name="catchall"/> is set.</summary>
internal sealed class ZodObject(IReadOnlyList<(string Name, ZodType Type)> shape, ZodType? catchall) : ZodType
{
    public IReadOnlyList<(string Name, ZodType Type)> Shape => shape;

    internal override ZodParse Parse(JsonNode? value, bool present, string path)
    {
        if (!present)
        {
            return Missing(path);
        }

        if (value is not JsonObject input)
        {
            return ZodParse.Fail(path, "expected object");
        }

        var result = new JsonObject();
        foreach (var (name, type) in shape)
        {
            var found = input.TryGetPropertyValue(name, out var property);
            var parsed = type.Parse(property, found, path + "." + name);
            if (!parsed.Success)
            {
                return parsed;
            }

            if (!parsed.Omit)
            {
                result[name] = parsed.Value;
            }
        }

        if (catchall is not null)
        {
            foreach (var (name, property) in input)
            {
                if (shape.Any(field => field.Name == name))
                {
                    continue;
                }

                var parsed = catchall.Parse(property, true, path + "." + name);
                if (!parsed.Success)
                {
                    return parsed;
                }

                result[name] = parsed.Value;
            }
        }

        return ZodParse.Ok(result);
    }

    internal override JsonObject BuildJsonSchema()
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, type) in shape)
        {
            properties[name] = type.ToJsonSchema();
            if (!type.IsOptional)
            {
                required.Add(name);
            }
        }

        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        schema["additionalProperties"] = catchall is null ? false : catchall.ToJsonSchema();
        return schema;
    }
}

internal sealed class ZodRecord(ZodType valueType) : ZodType
{
    internal override ZodParse Parse(JsonNode? value, bool present, string path)
    {
        if (!present)
        {
            return Missing(path);
        }

        if (value is not JsonObject input)
        {
            return ZodParse.Fail(path, "expected record");
        }

        var result = new JsonObject();
        foreach (var (name, property) in input)
        {
            var parsed = valueType.Parse(property, true, path + "." + name);
            if (!parsed.Success)
            {
                return parsed;
            }

            result[name] = parsed.Value;
        }

        return ZodParse.Ok(result);
    }

    internal override JsonObject BuildJsonSchema() =>
        new()
        {
            ["type"] = "object",
            ["propertyNames"] = new JsonObject { ["type"] = "string" },
            ["additionalProperties"] = valueType.ToJsonSchema(),
        };
}

internal sealed class ZodUnion(IReadOnlyList<ZodType> options) : ZodType
{
    internal override ZodParse Parse(JsonNode? value, bool present, string path)
    {
        foreach (var option in options)
        {
            var parsed = option.Parse(value, present, path);
            if (parsed.Success)
            {
                return parsed;
            }
        }

        return ZodParse.Fail(path, "invalid input");
    }

    internal override JsonObject BuildJsonSchema()
    {
        var anyOf = new JsonArray();
        foreach (var option in options)
        {
            anyOf.Add(option.ToJsonSchema());
        }

        return new JsonObject { ["anyOf"] = anyOf };
    }
}

/// <summary><c>z.optional</c>: the key may be absent. A null is not absent.</summary>
internal sealed class ZodOptional(ZodType inner) : ZodType
{
    internal override bool IsOptional => true;

    internal override ZodParse Parse(JsonNode? value, bool present, string path) =>
        present ? inner.Parse(value, true, path) : ZodParse.Omitted();

    internal override JsonObject BuildJsonSchema() => inner.ToJsonSchema();
}

internal sealed class ZodNullable(ZodType inner) : ZodType
{
    internal override ZodParse Parse(JsonNode? value, bool present, string path) =>
        present && value is null ? ZodParse.Ok(null) : inner.Parse(value, present, path);

    internal override JsonObject BuildJsonSchema() =>
        new() { ["anyOf"] = new JsonArray(inner.ToJsonSchema(), new JsonObject { ["type"] = "null" }) };
}

/// <summary><c>z.unknown</c> and <c>z.any</c>.</summary>
internal sealed class ZodUnknown : ZodType
{
    internal override ZodParse Parse(JsonNode? value, bool present, string path) => ZodParse.Ok(value?.DeepClone());

    internal override JsonObject BuildJsonSchema() => [];
}

/// <summary>.NET-only: the <c>z</c> object of <c>zod.ts</c> upstream, as far as the Gateway tool schemas use it (the rest of this class is in <c>Zod.cs</c>).</summary>
internal static partial class Zod
{
    public static ZodType Describe(ZodType schema, string description) => schema.Describe(description);

    public static ZodType String(int? minLength = null, int? maxLength = null) =>
        new ZodString(minLength, maxLength, false);

    public static ZodType Url() => new ZodString(null, null, true);

    public static ZodType Number(bool integer = false, double? minimum = null, double? maximum = null) =>
        new ZodNumber(integer, minimum, maximum);

    public static ZodType Boolean() => new ZodBoolean();

    public static ZodType Enum(params string[] values) => new ZodEnum(values);

    public static ZodType Array(ZodType item) => new ZodArray(item);

    public static ZodType Object(params (string Name, ZodType Type)[] shape) => new ZodObject(shape, null);

    public static ZodType Catchall(ZodType @object, ZodType catchall) =>
        new ZodObject(((ZodObject)@object).Shape, catchall);

    public static ZodType Record(ZodType valueType) => new ZodRecord(valueType);

    public static ZodType Union(params ZodType[] options) => new ZodUnion(options);

    public static ZodType Optional(ZodType inner) => new ZodOptional(inner);

    public static ZodType Nullable(ZodType inner) => new ZodNullable(inner);

    public static ZodType Nullish(ZodType inner) => new ZodOptional(new ZodNullable(inner));

    public static ZodType Unknown() => new ZodUnknown();

    public static ZodType Any() => new ZodUnknown();
}
