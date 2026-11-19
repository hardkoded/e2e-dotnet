// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.TypesafeAiDecisionModel;

/// <summary>The test server of <c>typesafe-ai-decision-model.test.ts</c> (<c>createTestServer</c>) and its fixtures.</summary>
internal sealed class TypeSafeServer : HttpMessageHandler
{
    public const string Url = "https://api.typesafe.ai/v1/systemone";

    public static readonly JsonNode NativeRequest = Fixture("decision-request.json");

    public static readonly JsonNode NativeResponse = Fixture("decision-response.json");

    public TypeSafeServer() => RespondJson(NativeResponse);

    public List<Call> Calls { get; } = [];

    public Action<CancellationToken>? OnSend { get; set; }

    private Func<HttpResponseMessage> Response { get; set; } = null!;

    /// <summary>The model of a provider that signs in with <c>test-api-key</c>.</summary>
    public DecisionTypeSafeAiModel Model(string modelId = "jev-latest") =>
        TypeSafeAi
            .Create(new TypeSafeAiProviderSettings { ApiKey = "test-api-key", HttpClient = new HttpClient(this) })
            .DecisionModel(modelId);

    public void RespondJson(JsonNode body) =>
        Response = () =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };

    public void RespondError(int status, string body) =>
        Response = () =>
            new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var header in request.Headers.Concat(request.Content!.Headers))
        {
            headers[header.Key.ToLowerInvariant()] = string.Join(", ", header.Value);
        }

        Calls.Add(
            new Call(
                request.RequestUri!.ToString(),
                headers,
                await request.Content.ReadAsByteArrayAsync(cancellationToken)
            )
        );
        OnSend?.Invoke(cancellationToken);
        return Response();
    }

    private static JsonNode Fixture(string name) =>
        JsonNode.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TypesafeAiDecisionModel", "Fixtures", name))
        )!;

    /// <summary>One request the server received.</summary>
    internal sealed record Call(string Url, Dictionary<string, string> Headers, byte[] Body)
    {
        public JsonNode Json() => JsonNode.Parse(Body)!;

        /// <summary>The parts of a <c>multipart/form-data</c> body, in order.</summary>
        public List<Part> Multipart()
        {
            const string marker = "boundary=";
            var contentType = Headers["content-type"];
            var boundary =
                "--" + contentType[(contentType.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..].Trim('"');
            // Latin-1 maps every byte to one character, so the split keeps binary parts intact.
            var text = Encoding.Latin1.GetString(Body);
            var parts = new List<Part>();
            foreach (var chunk in text.Split(boundary).Skip(1))
            {
                if (chunk.StartsWith("--", StringComparison.Ordinal))
                {
                    break;
                }

                var split = chunk.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                var head = chunk[..split];
                var data = chunk[(split + 4)..];
                data = data[..^2];
                string? Value(string key) =>
                    head.Split("\r\n")
                        .Select(line => line.Split(": ", 2))
                        .Where(pair => pair.Length == 2 && pair[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                        .Select(pair => pair[1])
                        .FirstOrDefault();
                string? Field(string name)
                {
                    // .NET writes a name or file name unquoted unless it needs quotes.
                    var disposition = Value("Content-Disposition") ?? string.Empty;
                    var match = System.Text.RegularExpressions.Regex.Match(
                        disposition,
                        "[ ;]" + name + "=(?:\"([^\"]*)\"|([^;]*))"
                    );
                    return !match.Success ? null
                        : match.Groups[1].Success ? match.Groups[1].Value
                        : match.Groups[2].Value;
                }

                parts.Add(
                    new Part(Field("name")!, Field("filename"), Value("Content-Type"), Encoding.Latin1.GetBytes(data))
                );
            }

            return parts;
        }

        /// <summary>The JSON of the <c>request</c> part of a multipart body.</summary>
        public JsonNode MultipartRequest() => JsonNode.Parse(Multipart().Single(part => part.Name == "request").Data)!;
    }

    internal sealed record Part(string Name, string? Filename, string? ContentType, byte[] Data);
}
