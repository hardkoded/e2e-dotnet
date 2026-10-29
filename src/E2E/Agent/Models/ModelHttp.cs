// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Internal;

/// <summary>The HTTP every model client shares: one POST, the provider's errors mapped to agent codes.</summary>
internal static class ModelHttp
{
    /// <summary><c>e2e-dotnet/&lt;version&gt; (&lt;platform&gt;; &lt;arch&gt;)</c>: what every vendor request identifies as. Never another client's name.</summary>
    public static readonly string UserAgent = "e2e-dotnet/" + PackageVersion() + " (" + Platform() + "; " + Arch() + ")";

    /// <summary>
    /// Sent with every model call, whichever provider serves it, replacing the same headers set on the client.
    /// The .NET runtime follows our user agent, as the AI SDK appends its own upstream; <c>HTTP-Referer</c> and
    /// <c>X-Title</c> are the app attribution the Vercel AI Gateway and OpenRouter read, and others ignore.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RequestHeaders = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["User-Agent"] = UserAgent + " runtime/dotnet/" + System.Environment.Version,
        ["HTTP-Referer"] = "https://github.com/hardkoded/e2e-dotnet",
        ["X-Title"] = "e2e-dotnet",
    };

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    /// <summary>The images a provider sends with a message: those of a <c>user</c> turn, none for any other role.</summary>
    public static IReadOnlyList<ModelImage> UserImages(ModelMessage message) =>
        string.Equals(message.Role, "user", StringComparison.Ordinal) ? message.Images ?? [] : [];

    /// <summary>An image as a <c>data:</c> URL, the form the OpenAI endpoints take.</summary>
    public static string DataUrl(ModelImage image) => "data:" + image.MediaType + ";base64," + Convert.ToBase64String(image.Data);

    /// <summary>The image format name Bedrock's Converse API takes: <c>png</c> for <c>image/png</c>, <c>jpeg</c> for <c>image/jpg</c>.</summary>
    public static string ImageFormat(ModelImage image)
    {
        var format = image.MediaType[(image.MediaType.IndexOf('/', StringComparison.Ordinal) + 1)..];
        return string.Equals(format, "jpg", StringComparison.OrdinalIgnoreCase) ? "jpeg" : format.ToLowerInvariant();
    }

    /// <summary>How a <c>CONTEXT_OVERFLOW</c> message opens; the agent inserts what its retry did after it.</summary>
    internal const string OverflowMessage = "The model request exceeded the context window";

    /// <summary>
    /// The finish reason the agent reads from the provider's own stop word: <see cref="ModelResponse.ContentFilter"/> when
    /// <paramref name="raw"/> is one of <paramref name="refusals"/>, else null.
    /// </summary>
    internal static string? FinishReason(string? raw, params string[] refusals) =>
        raw is not null && refusals.Contains(raw, StringComparer.Ordinal) ? ModelResponse.ContentFilter : null;

    /// <summary>A string property of <paramref name="element"/>, or null when it holds none.</summary>
    internal static string? OptionalString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>The most characters of a provider's finish reason a refusal message quotes.</summary>
    private const int MaxFinishReasonChars = 128;

    /// <summary>
    /// Upstream's <c>modelRefusalError</c> (<c>agent/model/sdk.ts</c>): <c>MODEL_REFUSED</c> naming the model and the
    /// provider's finish reason when it sent one. The reason is provider text, so it is redacted and cut.
    /// </summary>
    internal static AgentException ModelRefusalError(string model, string? rawFinishReason, Redactor redactor)
    {
        // Redact first and cut after, so no prefix of a secret survives the cut.
        var reason = string.IsNullOrWhiteSpace(rawFinishReason) ? ModelResponse.ContentFilter : redactor.Redact(rawFinishReason);
        if (reason.Length > MaxFinishReasonChars)
        {
            reason = reason[..MaxFinishReasonChars];
        }

        return new AgentException("MODEL_REFUSED", "the model " + model + " refused the request (" + reason + ")");
    }

    /// <summary>The most response body bytes a failure message quotes.</summary>
    private const int MaxProviderBodyBytes = 1024;

    /// <summary>The most body characters a failure message redacts, far past what it quotes.</summary>
    private const int MaxRedactedBodyChars = 16 * 1024;

    /// <summary>
    /// Sends <paramref name="message"/> and returns the body of a successful answer. A timeout, an unreachable
    /// host, or an error status fails with <c>MODEL_PROVIDER_FAILED</c>, or with a blocked <c>CONTEXT_OVERFLOW</c>
    /// when the provider says the request was too large. The failure names the HTTP status and
    /// the body, redacted with <paramref name="redactor"/> and cut at 1 KB. <paramref name="sign"/> runs on each attempt's copy just before
    /// it is sent, so a request that carries a timestamp (Bedrock's SigV4) is signed again for every retry, as the AI SDK signs inside each fetch.
    /// </summary>
    public static async Task<string> SendAsync(HttpClient http, HttpRequestMessage message, Redactor redactor, CancellationToken cancellationToken, Action<HttpRequestMessage>? sign = null)
    {
        ModelEndpoint.AddHeaders(message, RequestHeaders);
        // A request message can be sent once, so each attempt sends a copy of the same bytes.
        var payload = message.Content is null ? null : await message.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var backoff = InitialRetryDelay;
        for (var retry = 0; ; retry++)
        {
            using var attempt = CopyRequest(message, payload);
            sign?.Invoke(attempt);
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AgentException("MODEL_PROVIDER_FAILED", "The model request got no response within 120 seconds.");
            }
            catch (HttpRequestException ex)
            {
                // The AI SDK retries a fetch that failed before an answer (a refused or reset connection).
                if (retry < TransportRetries)
                {
                    await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
                    backoff *= RetryBackoffFactor;
                    continue;
                }

                throw new AgentException("MODEL_PROVIDER_FAILED", "The model provider could not be reached.", ex);
            }
            catch (OAuth.OAuthException ex)
            {
                // A missing or rejected subscription login stops the step; signing in again is the remedy.
                throw new AgentException("MODEL_PROVIDER_FAILED", "The subscription login failed (" + ex.Code + "): " + ex.Message, blocked: true, ex);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return body;
                }

                if (retry < TransportRetries && IsRetryableStatus((int)response.StatusCode))
                {
                    await Task.Delay(RetryDelay(response, backoff), cancellationToken).ConfigureAwait(false);
                    backoff *= RetryBackoffFactor;
                    continue;
                }

                var failure = FailureText(response, body, redactor);
                // A request too big is not an outage: the step's observations are what must shrink, and the code says so.
                if (ContextOverflow.DescribesOverflow((int)response.StatusCode, null, body))
                {
                    throw new AgentException("CONTEXT_OVERFLOW", OverflowMessage + ": " + failure, blocked: true);
                }

                throw new AgentException("MODEL_PROVIDER_FAILED", "The model provider failed: " + failure)
                {
                    ProviderStatus = (int)response.StatusCode,
                    ProviderErrorCode = ErrorCode(body),
                };
            }
        }
    }

    /// <summary>
    /// Upstream's <c>TRANSPORT_RETRIES</c> (<c>agent/model/sdk.ts</c>): how many times a model request is sent again after a retryable
    /// failure, so a request is sent at most six times. The AI SDK's own default is 2. Cancelling the call, or the step's deadline
    /// through its token, ends the wait between attempts.
    /// </summary>
    internal const int TransportRetries = 5;

    /// <summary>The AI SDK's first backoff: 2 seconds, doubled for each retry.</summary>
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

    private const int RetryBackoffFactor = 2;

    /// <summary>The AI SDK retries the statuses a provider marks retryable: 408, 409, 429, and every 5xx.</summary>
    private static bool IsRetryableStatus(int status) => status is 408 or 409 or 429 or >= 500;

    /// <summary>
    /// The AI SDK's <c>getRetryDelayInMs</c>: the provider's <c>retry-after-ms</c>, else its <c>retry-after</c> (seconds or an HTTP date),
    /// when it is not negative and under a minute (or under the backoff); otherwise the exponential backoff.
    /// </summary>
    private static TimeSpan RetryDelay(HttpResponseMessage response, TimeSpan backoff)
    {
        double? retryAfterMs = null;
        if (response.Headers.TryGetValues("retry-after-ms", out var millis) && double.TryParse(millis.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMs))
        {
            retryAfterMs = parsedMs;
        }

        if (retryAfterMs is null && response.Headers.TryGetValues("retry-after", out var after))
        {
            var value = after.FirstOrDefault();
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                retryAfterMs = seconds * 1000;
            }
            else if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                retryAfterMs = (date - DateTimeOffset.UtcNow).TotalMilliseconds;
            }
        }

        return retryAfterMs is { } ms && double.IsFinite(ms) && ms >= 0 && (ms < 60_000 || ms < backoff.TotalMilliseconds)
            ? TimeSpan.FromMilliseconds(ms)
            : backoff;
    }

    private static HttpRequestMessage CopyRequest(HttpRequestMessage message, byte[]? payload)
    {
        var copy = new HttpRequestMessage(message.Method, message.RequestUri) { Version = message.Version, VersionPolicy = message.VersionPolicy };
        foreach (var header in message.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in message.Options)
        {
            ((IDictionary<string, object?>)copy.Options)[option.Key] = option.Value;
        }

        if (payload is not null)
        {
            copy.Content = new ByteArrayContent(payload);
            foreach (var header in message.Content!.Headers)
            {
                copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return copy;
    }

    /// <summary>The <c>error.code</c> a JSON error body carries when it is a short string; null for any other body.</summary>
    private static string? ErrorCode(string body)
    {
        try
        {
            var code = (JsonNode.Parse(body) as JsonObject)?["error"] is JsonObject error ? error["code"] as JsonValue : null;
            return code is not null && code.TryGetValue<string>(out var text) && text.Length <= 128 ? text : null;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // A body with a duplicate property name is not an error the code can be read from.
            return null;
        }
    }

    /// <summary>The failure of a response the provider answered 200 and then failed: a blocked <c>CONTEXT_OVERFLOW</c> when it says the request was too large. The text is redacted, as an HTTP failure body is.</summary>
    public static AgentException ResponseFailed(string error, Redactor redactor)
    {
        error = redactor.Redact(error);
        return ContextOverflow.DescribesOverflow(null, error, null)
            ? new AgentException("CONTEXT_OVERFLOW", OverflowMessage + ": " + error, blocked: true)
            : new AgentException("MODEL_PROVIDER_FAILED", "The model provider failed the response. " + error);
    }

    /// <summary>
    /// <c>HTTP 400</c> or <c>HTTP 400: {body}</c>. The body is redacted before it is cut, so no prefix of a
    /// secret survives the cut. A body past <see cref="MaxRedactedBodyChars"/> is cut first, so a large error
    /// page does not cost a scan of all of it.
    /// </summary>
    private static string FailureText(HttpResponseMessage response, string body, Redactor redactor)
    {
        var status = "HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        var trimmed = body.Trim();
        var redacted = redactor.Redact(trimmed.Length > MaxRedactedBodyChars ? trimmed[..MaxRedactedBodyChars] : trimmed);
        if (redacted.Length == 0)
        {
            return status;
        }

        var quoted = CutUtf8(redacted, MaxProviderBodyBytes);
        return status + ": " + quoted + (quoted.Length < redacted.Length ? "…" : "");
    }

    /// <summary>The longest prefix of <paramref name="text"/> that fits in <paramref name="maxBytes"/> of UTF-8, never splitting a character.</summary>
    private static string CutUtf8(string text, int maxBytes)
    {
        var bytes = 0;
        var end = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            bytes += rune.Utf8SequenceLength;
            if (bytes > maxBytes)
            {
                break;
            }

            end += rune.Utf16SequenceLength;
        }

        return text[..end];
    }

    /// <summary>Parses a provider answer, mapping unreadable JSON to <c>MODEL_OUTPUT_INVALID</c>.</summary>
    public static ModelResponse Parse(string body, Func<JsonElement, ModelResponse> parse)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return parse(document.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or FormatException)
        {
            throw new AgentException("MODEL_OUTPUT_INVALID", "The model provider returned JSON that could not be read.", ex);
        }
    }

    /// <summary>
    /// The tool a provider with no structured-output field is made to call to answer with <see cref="ModelRequest.Schema"/>
    /// (the AI SDK's <c>json</c> tool).
    /// </summary>
    public const string JsonToolName = "json";

    public static string JsonToolDescription(ModelRequest request) => "Respond with a JSON object" + (request.SchemaName is null ? "." : " named " + request.SchemaName + ".");

    /// <summary>
    /// A structured answer given as the forced <see cref="JsonToolName"/> tool call, as the content of the turn: the arguments, as text.
    /// A turn that made no such call comes back as it is.
    /// </summary>
    public static ModelResponse StructuredAnswer(ModelResponse response)
    {
        var call = response.ToolCalls.FirstOrDefault(item => string.Equals(item.Name, JsonToolName, StringComparison.Ordinal));
        if (call is null)
        {
            return response;
        }

        return new ModelResponse
        {
            Content = call.Arguments.GetRawText(),
            Usage = response.Usage,
            ReasoningItems = response.ReasoningItems,
            FinishReason = response.FinishReason,
            RawFinishReason = response.RawFinishReason,
        };
    }

    public static StringContent Json(JsonNode payload)
    {
        return new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// Tool arguments as a JSON element; an empty string is an empty object. Text that is not valid JSON, such as a
    /// truncated answer, comes back as a JSON string of that text: the act loop fails that tool call and the model sees the
    /// failure, instead of the whole step ending on one bad call.
    /// </summary>
    public static JsonElement Arguments(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "{}";
        }

        try
        {
            using var parsed = JsonDocument.Parse(text);
            return parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(text);
        }
    }

    /// <summary>The argument object a provider that takes objects is sent back: the JSON, or an empty object when the call did not carry one.</summary>
    public static JsonNode ArgumentsObject(JsonElement arguments) =>
        arguments.ValueKind == JsonValueKind.Object ? JsonNode.Parse(arguments.GetRawText())! : new JsonObject();

    /// <summary>The argument text a provider is sent back: the text of an argument that did not parse, else the JSON.</summary>
    public static string ArgumentsText(JsonElement arguments) =>
        arguments.ValueKind == JsonValueKind.String ? arguments.GetString() ?? "" : arguments.GetRawText();

    /// <summary>
    /// Adds the agent's provider options under <paramref name="provider"/> to <paramref name="payload"/>, as
    /// given. They cannot replace a field the client itself sets.
    /// </summary>
    public static void MergeProviderOptions(JsonObject payload, ModelRequest request, string provider, params string[] reserved)
    {
        if (request.ProviderOptions is null || !request.ProviderOptions.TryGetValue(provider, out var extra))
        {
            return;
        }

        if (extra.ValueKind != JsonValueKind.Object)
        {
            throw new ConfigurationException("INVALID_CONFIG", "providerOptions." + provider + " must be an object of provider options");
        }

        foreach (var field in extra.EnumerateObject())
        {
            if (reserved.Contains(field.Name, StringComparer.Ordinal))
            {
                throw new ConfigurationException("INVALID_CONFIG", "providerOptions." + provider + "." + field.Name + " cannot replace a field the client sets");
            }

            payload[field.Name] = JsonNode.Parse(field.Value.GetRawText());
        }
    }

    /// <summary>A stable key for one system prompt, so every call sharing the prefix routes to the same provider cache.</summary>
    public static string PromptCacheKey(string system)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(system));
        return "e2e-" + Convert.ToHexStringLower(hash)[..16];
    }

    /// <summary>The value of <paramref name="name"/> in the environment, or null when it is unset or blank.</summary>
    public static string? Environment(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var value = System.Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>The tool name each earlier tool call id was made with, for protocols that answer a call by name.</summary>
    public static Dictionary<string, string> ToolNames(IReadOnlyList<ModelMessage> messages)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var message in messages)
        {
            foreach (var call in message.ToolCalls ?? [])
            {
                names[call.Id] = call.Name;
            }
        }

        return names;
    }

    /// <summary>The package version MinVer stamps, without its build metadata.</summary>
    internal static string PackageVersion()
    {
        var version = typeof(ModelHttp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        return version.Split('+')[0];
    }

    /// <summary>The operating system by Node's <c>process.platform</c> name, as upstream reports it.</summary>
    private static string Platform()
    {
        return OperatingSystem.IsMacOS() ? "darwin"
            : OperatingSystem.IsWindows() ? "win32"
            : OperatingSystem.IsLinux() ? "linux"
            : OperatingSystem.IsFreeBSD() ? "freebsd"
            : "unknown";
    }

    /// <summary>The process architecture by Node's <c>process.arch</c> name.</summary>
    private static string Arch()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "ia32",
            Architecture.LoongArch64 => "loong64",
            Architecture.Ppc64le => "ppc64",
            var other => other.ToString().ToLowerInvariant(),
        };
    }

    /// <summary>A token count the provider may leave out: null when the property is missing or not a count.</summary>
    public static int? OptionalInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= 0 ? number : null;
    }

    /// <summary>A cost in US dollars the provider may leave out: null when the property is missing or not a finite, non-negative number.</summary>
    public static double? OptionalCost(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var cost) && double.IsFinite(cost) && cost >= 0 ? cost : null;
    }

    /// <summary>The object property <paramref name="name"/> of <paramref name="element"/>, or null when it is missing or not an object.</summary>
    public static JsonElement? Details(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;
    }

    public static int Int(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
    }
}
