// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>
/// .NET-only: a value that TypeScript types as <c>T | null | undefined</c>. The default is <c>undefined</c> (left out of the JSON), and
/// <see cref="Null"/> is an explicit <c>null</c>.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
[JsonConverter(typeof(UndefinableConverterFactory))]
public readonly struct Undefinable<T>
{
    private Undefinable(T? value, bool isNull)
    {
        IsDefined = true;
        IsNull = isNull;
        Value = value;
    }

    /// <summary>False for <c>undefined</c>.</summary>
    public bool IsDefined { get; }

    /// <summary>True for <see cref="Null"/>.</summary>
    public bool IsNull { get; }

    /// <summary>The value. Meaningless for <see cref="Null"/> and for <c>undefined</c>.</summary>
    public T? Value { get; }

    /// <summary>An explicit <c>null</c>.</summary>
    public static Undefinable<T> Null => new(default, true);

    /// <summary>A value.</summary>
    public static implicit operator Undefinable<T>(T? value) => new(value, value is null);
}

internal sealed class UndefinableConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Undefinable<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)
            Activator.CreateInstance(
                typeof(UndefinableConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0])
            )!;
}

internal sealed class UndefinableConverter<T> : JsonConverter<Undefinable<T>>
{
    public override bool HandleNull => true;

    public override Undefinable<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null
            ? Undefinable<T>.Null
            : JsonSerializer.Deserialize<T>(ref reader, options);

    public override void Write(Utf8JsonWriter writer, Undefinable<T> value, JsonSerializerOptions options)
    {
        if (value.IsNull)
        {
            writer.WriteNullValue();
        }
        else
        {
            JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}

/// <summary>.NET-only: TypeScript's <c>string | string[]</c>.</summary>
[JsonConverter(typeof(StringOrStringsConverter))]
public sealed class StringOrStrings
{
    private StringOrStrings(string? text, IReadOnlyList<string>? items)
    {
        Text = text;
        Items = items;
    }

    /// <summary>The string. Null when this is a list.</summary>
    public string? Text { get; }

    /// <summary>The list. Null when this is a string.</summary>
    public IReadOnlyList<string>? Items { get; }

    /// <summary>A string.</summary>
    public static implicit operator StringOrStrings(string text) => new(text, null);

    /// <summary>A list of strings.</summary>
    public static implicit operator StringOrStrings(string[] items) => new(null, items);
}

internal sealed class StringOrStringsConverter : JsonConverter<StringOrStrings>
{
    public override StringOrStrings? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) =>
        reader.TokenType == JsonTokenType.String
            ? (StringOrStrings)reader.GetString()!
            : (StringOrStrings)JsonSerializer.Deserialize<string[]>(ref reader, options)!;

    public override void Write(Utf8JsonWriter writer, StringOrStrings value, JsonSerializerOptions options)
    {
        if (value.Text is not null)
        {
            writer.WriteStringValue(value.Text);
        }
        else
        {
            JsonSerializer.Serialize(writer, value.Items, options);
        }
    }
}

/// <summary>.NET-only: TypeScript's <c>boolean | T</c>.</summary>
/// <typeparam name="T">The object type.</typeparam>
[JsonConverter(typeof(BooleanOrConverterFactory))]
public sealed class BooleanOr<T>
    where T : class
{
    private BooleanOr(bool? flag, T? value)
    {
        Flag = flag;
        Value = value;
    }

    /// <summary>The boolean. Null when this is an object.</summary>
    public bool? Flag { get; }

    /// <summary>The object. Null when this is a boolean.</summary>
    public T? Value { get; }

    /// <summary>A boolean.</summary>
    public static implicit operator BooleanOr<T>(bool flag) => new(flag, null);

    /// <summary>An object.</summary>
    public static implicit operator BooleanOr<T>(T value) => new(null, value);
}

internal sealed class BooleanOrConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(BooleanOr<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)
            Activator.CreateInstance(
                typeof(BooleanOrConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0])
            )!;
}

internal sealed class BooleanOrConverter<T> : JsonConverter<BooleanOr<T>>
    where T : class
{
    public override BooleanOr<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType is JsonTokenType.True or JsonTokenType.False
            ? (BooleanOr<T>)reader.GetBoolean()
            : (BooleanOr<T>)JsonSerializer.Deserialize<T>(ref reader, options)!;

    public override void Write(Utf8JsonWriter writer, BooleanOr<T> value, JsonSerializerOptions options)
    {
        if (value.Flag is { } flag)
        {
            writer.WriteBooleanValue(flag);
        }
        else
        {
            JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}
