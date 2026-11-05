// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using static E2E.Tests.CheckActions.CheckPage;
using EngineEvaluation = E2E.Engine.Evaluation;

namespace E2E.Tests.Evaluation;

/// <summary>
/// <c>describe('pageValueEncoder')</c> of upstream's <c>evaluation.test.ts</c>. The encoder is page source here, so each case builds its value
/// in a live page and reads back the JSON text it encodes, or the reason it refuses.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class PageValueEncoderTests
{
    /// <summary>Encodes the value <paramref name="valueSource"/> builds in the page: the text, or the reason the encoder threw.</summary>
    private static async Task<(string? Text, string? Invalid)> EncodeAsync(IEngineSession session, string valueSource)
    {
        var result = await ((IBrowserSession)session).EvaluateAsync(
            $$"""
            () => {
              const encode = ({{EngineEvaluation.PageValueEncoder}})();
              try {
                return JSON.stringify({ text: encode(({{valueSource}})) });
              } catch (cause) {
                return JSON.stringify({ invalid: cause.invalid });
              }
            }
            """,
            null,
            hasArg: false,
            CancellationToken.None);
        using var parsed = JsonDocument.Parse(result!.Value.GetString()!);
        var value = parsed.RootElement;
        return (
            value.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null,
            value.TryGetProperty("invalid", out var invalid) ? invalid.GetString() : null);
    }

    [Theory]
    [InlineData("({ a: [1, 'b', null, true], c: { d: -1.5 } })")]
    [InlineData("'tab\\t \"quoted\" \\\\ \\u0000 \\u2028 \\ud83d\\ude00'")]
    [InlineData("[]")]
    [InlineData("({})")]
    public async Task Encodes_JSON_text_that_parses_back_to_the_value(string valueSource)
    {
        await RunAsync(string.Empty, async session =>
        {
            var expected = await ((IBrowserSession)session).EvaluateAsync("() => JSON.stringify(" + valueSource + ")", null, hasArg: false, CancellationToken.None);
            var (text, invalid) = await EncodeAsync(session, valueSource);
            Assert.Null(invalid);
            Assert.NotNull(text);
            Assert.Equal(
                JsonSerializer.Serialize(JsonDocument.Parse(expected!.Value.GetString()!).RootElement),
                JsonSerializer.Serialize(JsonDocument.Parse(text).RootElement));
        });
    }

    [Fact]
    public async Task Keeps_a_lone_surrogate()
    {
        await RunAsync(string.Empty, async session =>
        {
            var (text, _) = await EncodeAsync(session, "'\\ud800'");
            Assert.Equal("\"\\ud800\"", text);
        });
    }

    [Theory]
    [InlineData("NaN", "Number.NaN", "contains a non-finite number")]
    [InlineData("a Date", "({ when: new Date(0) })", "must be JSON-safe")]
    [InlineData("a Map", "[new Map()]", "must be JSON-safe")]
    [InlineData("an object with toJSON", "new (class { toJSON() { return 1; } })()", "must be JSON-safe")]
    [InlineData("a bigint", "({ id: 1n })", "must be JSON-safe, found bigint")]
    [InlineData("a symbol", "[Symbol('x')]", "must be JSON-safe, found symbol")]
    [InlineData("an undefined member", "({ missing: undefined })", "must be JSON-safe, found undefined")]
    [InlineData("a function", "(() => 1)", "must be JSON-safe, found function")]
    public async Task Rejects(string label, string valueSource, string invalid)
    {
        Assert.NotEmpty(label);
        await RunAsync(string.Empty, async session =>
        {
            var (text, reason) = await EncodeAsync(session, valueSource);
            Assert.Null(text);
            Assert.Equal(invalid, reason);
        });
    }

    [Fact]
    public async Task Rejects_a_cycle_but_not_a_shared_object()
    {
        await RunAsync(string.Empty, async session =>
        {
            var (shared, sharedInvalid) = await EncodeAsync(session, "(() => { const shared = { sku: 'a' }; return [shared, shared]; })()");
            Assert.Null(sharedInvalid);
            Assert.Equal("[{\"sku\":\"a\"},{\"sku\":\"a\"}]", shared);
            var (_, cycle) = await EncodeAsync(session, "(() => { const cyclic = {}; cyclic.self = cyclic; return cyclic; })()");
            Assert.Equal("contains a cycle", cycle);
        });
    }

    [Fact]
    public async Task Encodes_a_null_prototype_object()
    {
        await RunAsync(string.Empty, async session =>
        {
            var (text, _) = await EncodeAsync(session, "Object.assign(Object.create(null), { sku: 'a' })");
            Assert.Equal("{\"sku\":\"a\"}", text);
        });
    }

    [Fact]
    public async Task Returns_no_text_for_an_undefined_root()
    {
        await RunAsync(string.Empty, async session =>
        {
            var (text, invalid) = await EncodeAsync(session, "undefined");
            Assert.Null(text);
            Assert.Null(invalid);
        });
    }
}
