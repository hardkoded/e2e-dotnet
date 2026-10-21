// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace E2E.Tests.TypedReport;

/// <summary>
/// Writes a typed report back as the JSON document it was read from, so a test can compare the two. The library only reads a report: the
/// document is built as JSON, so nothing in production writes the typed form.
/// </summary>
internal static class ReportJsonWriter
{
    private static readonly JsonSerializerOptions Options = Create();

    /// <summary>The document of <paramref name="report"/>, or any part of it, as JSON text. It has no <c>serialGroups</c> member, which the schema requires.</summary>
    public static string Write<T>(T report) => JsonSerializer.Serialize(report, Options);

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(ReportSerializer.Options) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        options.Converters.Insert(0, new TimestampConverter());
        options.Converters.Insert(0, new ReportExploreWriter());
        return options;
    }

    /// <summary>A moment in the spelling the schema requires: UTC with milliseconds.</summary>
    private sealed class TimestampConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDateTimeOffset();

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => writer.WriteStringValue(ReportWire.Timestamp(value));
    }

    private sealed class ReportExploreWriter : JsonConverter<ReportExplore>
    {
        public override ReportExplore Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, ReportExplore value, JsonSerializerOptions options) => ReportWire.Explore(value).WriteTo(writer);
    }
}
