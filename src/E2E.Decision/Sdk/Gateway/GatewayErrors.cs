// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>GatewayError</c> (<c>packages/gateway/src/errors</c>): a failure of the Vercel AI Gateway.</summary>
public abstract class GatewayException : Exception
{
    protected GatewayException(
        string message,
        int statusCode,
        Exception? cause,
        string? generationId,
        bool? isRetryable = null
    )
        : base(generationId is null ? message : message + " [" + generationId + "]", cause)
    {
        StatusCode = statusCode;
        GenerationId = generationId;
        IsRetryable = isRetryable ?? statusCode is 408 or 409 or 429 or >= 500;
    }

    /// <summary>The error's class name upstream, such as <c>GatewayInvalidRequestError</c>.</summary>
    public abstract string Name { get; }

    /// <summary>The error type the Gateway reports, such as <c>invalid_request_error</c>.</summary>
    public abstract string Type { get; }

    public int StatusCode { get; }

    public string? GenerationId { get; }

    public bool IsRetryable { get; }
}

/// <summary>Authentication failed: an invalid API key or OIDC token.</summary>
public sealed class GatewayAuthenticationException : GatewayException
{
    public GatewayAuthenticationException(
        string message = "Authentication failed",
        int statusCode = 401,
        Exception? cause = null,
        string? generationId = null
    )
        : base(message, statusCode, cause, generationId) { }

    public override string Name => "GatewayAuthenticationError";

    public override string Type => "authentication_error";

    /// <summary>Upstream's <c>createContextualError</c>: the message names how to authenticate.</summary>
    public static GatewayAuthenticationException CreateContextualError(
        bool apiKeyProvided,
        bool oidcTokenProvided,
        int statusCode = 401,
        Exception? cause = null,
        string? generationId = null
    )
    {
        string message;
        if (apiKeyProvided)
        {
            message =
                "AI Gateway authentication failed: Invalid API key or token.\n\n"
                + "Create a new API key: https://vercel.com/d?to=%2F%5Bteam%5D%2F%7E%2Fai%2Fapi-keys\n\n"
                + "Provide an API key or Vercel access token via 'apiKey' option or 'AI_GATEWAY_API_KEY' environment variable.";
        }
        else if (oidcTokenProvided)
        {
            message =
                "AI Gateway authentication failed: Invalid OIDC token.\n\n"
                + "Run 'npx vercel link' to link your project, then 'vc env pull' to fetch the token.\n\n"
                + "Alternatively, use an API key: https://vercel.com/d?to=%2F%5Bteam%5D%2F%7E%2Fai%2Fapi-keys\n"
                + "or pass a Vercel access token via the 'apiKey' option.";
        }
        else
        {
            message =
                "AI Gateway authentication failed: No authentication provided.\n\n"
                + "Option 1 - API key:\n"
                + "Create an API key: https://vercel.com/d?to=%2F%5Bteam%5D%2F%7E%2Fai%2Fapi-keys\n"
                + "Provide via 'apiKey' option or 'AI_GATEWAY_API_KEY' environment variable.\n\n"
                + "Option 2 - Vercel access token:\n"
                + "Pass a Vercel personal access token or Vercel app access token via the 'apiKey' option.\n\n"
                + "Option 3 - OIDC token:\n"
                + "Run 'npx vercel link' to link your project, then 'vc env pull' to fetch the token.";
        }

        return new GatewayAuthenticationException(message, statusCode, cause, generationId);
    }
}

/// <summary>Invalid request: missing headers, malformed data, and so on.</summary>
public sealed class GatewayInvalidRequestException : GatewayException
{
    public GatewayInvalidRequestException(
        string message = "Invalid request",
        int statusCode = 400,
        Exception? cause = null,
        string? generationId = null
    )
        : base(message, statusCode, cause, generationId) { }

    public override string Name => "GatewayInvalidRequestError";

    public override string Type => "invalid_request_error";
}

/// <summary>Rate limit exceeded.</summary>
public sealed class GatewayRateLimitException : GatewayException
{
    public GatewayRateLimitException(
        string message = "Rate limit exceeded",
        int statusCode = 429,
        Exception? cause = null,
        string? generationId = null
    )
        : base(message, statusCode, cause, generationId) { }

    public override string Name => "GatewayRateLimitError";

    public override string Type => "rate_limit_exceeded";
}

/// <summary>Model not found or not available.</summary>
public sealed class GatewayModelNotFoundException : GatewayException
{
    public GatewayModelNotFoundException(
        string message = "Model not found",
        int statusCode = 404,
        string? modelId = null,
        Exception? cause = null,
        string? generationId = null
    )
        : base(message, statusCode, cause, generationId)
    {
        ModelId = modelId;
    }

    public override string Name => "GatewayModelNotFoundError";

    public override string Type => "model_not_found";

    public string? ModelId { get; }
}

/// <summary>Resource not found.</summary>
public sealed class GatewayNotFoundException : GatewayException
{
    public GatewayNotFoundException(
        string message = "Resource not found",
        int statusCode = 404,
        Exception? cause = null,
        string? generationId = null
    )
        : base(message, statusCode, cause, generationId) { }

    public override string Name => "GatewayNotFoundError";

    public override string Type => "not_found";
}

/// <summary>Internal server error from the Gateway.</summary>
public sealed class GatewayInternalServerException : GatewayException
{
    public GatewayInternalServerException(
        string message = "Internal server error",
        int statusCode = 500,
        Exception? cause = null,
        string? generationId = null
    )
        : base(message, statusCode, cause, generationId) { }

    public override string Name => "GatewayInternalServerError";

    public override string Type => "internal_server_error";
}

/// <summary>Failed dependency.</summary>
public sealed class GatewayFailedDependencyException : GatewayException
{
    public GatewayFailedDependencyException(
        string message = "Failed dependency",
        int statusCode = 424,
        Exception? cause = null,
        string? generationId = null
    )
        : base(message, statusCode, cause, generationId) { }

    public override string Name => "GatewayFailedDependencyError";

    public override string Type => "failed_dependency";
}

/// <summary>Forbidden: the request was rejected by policy (for example a routing rule), not an authentication failure.</summary>
public sealed class GatewayForbiddenException : GatewayException
{
    public GatewayForbiddenException(
        string message = "Forbidden",
        int statusCode = 403,
        Exception? cause = null,
        string? generationId = null,
        string? ruleId = null
    )
        : base(message, statusCode, cause, generationId)
    {
        RuleId = ruleId;
    }

    public override string Name => "GatewayForbiddenError";

    public override string Type => "forbidden";

    public string? RuleId { get; }
}

/// <summary>Access policy denied: an AI Gateway access policy does not allow the requested model or provider.</summary>
public sealed class GatewayAccessPolicyDeniedException : GatewayException
{
    public GatewayAccessPolicyDeniedException(
        string message = "Access policy denied",
        int statusCode = 403,
        Exception? cause = null,
        string? generationId = null,
        IReadOnlyList<string>? policyIds = null,
        IReadOnlyList<string>? removedModels = null,
        IReadOnlyList<string>? removedProviders = null
    )
        : base(message, statusCode, cause, generationId)
    {
        PolicyIds = policyIds;
        RemovedModels = removedModels;
        RemovedProviders = removedProviders;
    }

    public override string Name => "GatewayAccessPolicyDeniedError";

    public override string Type => "access_policy_denied";

    public IReadOnlyList<string>? PolicyIds { get; }

    public IReadOnlyList<string>? RemovedModels { get; }

    public IReadOnlyList<string>? RemovedProviders { get; }
}

/// <summary>Client request timed out before receiving a response.</summary>
public sealed class GatewayTimeoutException : GatewayException
{
    public GatewayTimeoutException(
        string message = "Request timed out",
        int statusCode = 408,
        Exception? cause = null,
        string? generationId = null
    )
        : base(message, statusCode, cause, generationId) { }

    public override string Name => "GatewayTimeoutError";

    public override string Type => "timeout_error";

    /// <summary>Upstream's <c>createTimeoutError</c>.</summary>
    public static GatewayTimeoutException CreateTimeoutError(
        string originalMessage,
        int statusCode = 408,
        Exception? cause = null,
        string? generationId = null
    ) =>
        new(
            "Gateway request timed out: "
                + originalMessage
                + "\n\n    This is a client-side timeout. To resolve this, increase your timeout configuration: "
                + "https://vercel.com/docs/ai-gateway/capabilities/video-generation#extending-timeouts-for-node.js",
            statusCode,
            cause,
            generationId
        );
}

/// <summary>Gateway response parsing error: the error body is not the shape the Gateway documents.</summary>
public sealed class GatewayResponseException : GatewayException
{
    public GatewayResponseException(
        string message = "Invalid response from Gateway",
        int statusCode = 502,
        string? response = null,
        Exception? cause = null,
        string? generationId = null,
        bool? isRetryable = null,
        string? validationError = null
    )
        : base(message, statusCode, cause, generationId, isRetryable)
    {
        Response = response;
        ValidationError = validationError;
    }

    public override string Name => "GatewayResponseError";

    public override string Type => "response_error";

    /// <summary>The body that did not parse, as text: a JSON string is its text, JSON null is null, anything else is its JSON.</summary>
    public string? Response { get; }

    /// <summary>Why the body is not the shape the Gateway documents (upstream keeps the Zod error).</summary>
    public string? ValidationError { get; }
}

/// <summary>The AI SDK's <c>createGatewayErrorFromResponse</c> and <c>asGatewayError</c>.</summary>
public static class GatewayErrors
{
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement;

    /// <summary>Upstream's <c>createGatewayErrorFromResponse</c>: the Gateway's error type picks the error.</summary>
    public static GatewayException CreateGatewayErrorFromResponse(
        JsonElement? response,
        int statusCode,
        string defaultMessage = "Gateway request failed",
        Exception? cause = null,
        string? authMethod = null,
        bool? isRetryable = null
    )
    {
        var validationError = Validate(response, out var root, out var error, out var messageElement);
        if (validationError is not null)
        {
            return new GatewayResponseException(
                "Invalid error response format: " + defaultMessage,
                statusCode,
                response switch
                {
                    { ValueKind: JsonValueKind.String } text => text.GetString(),
                    null or { ValueKind: JsonValueKind.Null } => null,
                    { } other => other.GetRawText(),
                },
                cause,
                response is { ValueKind: JsonValueKind.Object } body ? String(body, "generationId") : null,
                isRetryable,
                validationError
            );
        }

        var message = messageElement.GetString()!;
        var generationId = String(root, "generationId");
        error.TryGetProperty("param", out var param);
        switch (String(error, "type"))
        {
            case "authentication_error":
                return GatewayAuthenticationException.CreateContextualError(
                    authMethod == "api-key",
                    authMethod == "oidc",
                    statusCode,
                    cause,
                    generationId
                );
            case "invalid_request_error":
                return new GatewayInvalidRequestException(message, statusCode, cause, generationId);
            case "rate_limit_exceeded":
                return new GatewayRateLimitException(message, statusCode, cause, generationId);
            case "model_not_found":
                return new GatewayModelNotFoundException(
                    message,
                    statusCode,
                    String(param, "modelId"),
                    cause,
                    generationId
                );
            case "not_found":
                return new GatewayNotFoundException(message, statusCode, cause, generationId);
            case "failed_dependency":
                return new GatewayFailedDependencyException(message, statusCode, cause, generationId);
            case "forbidden":
                return new GatewayForbiddenException(message, statusCode, cause, generationId, String(param, "ruleId"));
            case "access_policy_denied":
                return new GatewayAccessPolicyDeniedException(
                    message,
                    statusCode,
                    cause,
                    generationId,
                    Strings(param, "policyIds"),
                    Strings(param, "removedModels"),
                    Strings(param, "removedProviders")
                );
            default:
                return new GatewayInternalServerException(message, statusCode, cause, generationId);
        }
    }

    /// <summary>
    /// Upstream's <c>asGatewayError</c>: a Gateway error stays; a client timeout becomes a timeout error; a failed call becomes the error
    /// its body names; anything else, including a value that is not an exception, is a 500. A caller's cancellation is not a Gateway failure and is not wrapped.
    /// </summary>
    public static GatewayException AsGatewayError(object? error, string? authMethod = null)
    {
        // Caller cancellation is not a gateway failure and must not become a retryable 500.
        if (error is OperationCanceledException canceled && !IsTimeoutError(canceled))
        {
            ExceptionDispatchInfo.Capture(canceled).Throw();
        }

        if (error is GatewayException gateway)
        {
            return gateway;
        }

        if (error is Exception exception && IsTimeoutError(exception))
        {
            return GatewayTimeoutException.CreateTimeoutError(exception.Message, cause: exception);
        }

        if (error is ApiCallException api)
        {
            if (api.InnerException is { } inner && IsTimeoutError(inner))
            {
                return GatewayTimeoutException.CreateTimeoutError(api.Message, cause: api);
            }

            return CreateGatewayErrorFromResponse(
                ExtractApiCallResponse(api),
                api.StatusCode ?? 500,
                "Gateway request failed",
                api,
                authMethod,
                api.IsRetryable && (api.StatusCode is null || api.StatusCode < 400) ? true : null
            );
        }

        return CreateGatewayErrorFromResponse(
            EmptyObject,
            500,
            error is Exception cause ? "Gateway request failed: " + cause.Message : "Unknown Gateway error",
            error as Exception,
            authMethod
        );
    }

    /// <summary>
    /// Upstream's <c>parseAuthMethod</c> (<c>parse-auth-method.ts</c>): the <c>ai-gateway-auth-method</c> header when it is exactly
    /// <c>api-key</c> or <c>oidc</c>, else null.
    /// </summary>
    internal static string? ParseAuthMethod(IReadOnlyDictionary<string, string?>? headers)
    {
        var method = headers
            ?.FirstOrDefault(pair =>
                string.Equals(pair.Key, GatewayHeaders.GatewayAuthMethodHeader, StringComparison.OrdinalIgnoreCase)
            )
            .Value;
        return method is "api-key" or "oidc" ? method : null;
    }

    /// <summary>
    /// Upstream's <c>extractApiCallResponse</c> (<c>extract-api-call-response.ts</c>): the parsed <c>data</c> when the call has it, else
    /// the body parsed as JSON, else the raw body text, else an empty object. A body with a prototype-polluting key is not parsed
    /// (<c>secureJsonParse</c>).
    /// </summary>
    internal static JsonElement? ExtractApiCallResponse(ApiCallException error)
    {
        if (error.ParsedData is not null)
        {
            return error.ParsedData;
        }

        if (error.ResponseBody is null)
        {
            return EmptyObject;
        }

        // secureJsonParse: a body that is not JSON, or carries a forbidden key, stays the raw text.
        return TryParseSecureJson(error.ResponseBody, out var parsed)
            ? parsed
            : JsonSerializer.SerializeToElement(error.ResponseBody);
    }

    private static bool TryParseSecureJson(string text, out JsonElement parsed)
    {
        parsed = default;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            if (HasForbiddenKey(document.RootElement))
            {
                return false;
            }

            parsed = document.RootElement.Clone();
            return true;
        }
    }

    // isTimeoutError: upstream matches undici's timeout codes; .NET's HttpClient timeout is a TimeoutException, or a cancellation caused by one.
    private static bool IsTimeoutError(Exception error) =>
        error is TimeoutException || (error is OperationCanceledException && error.InnerException is TimeoutException);

    private static bool HasForbiddenKey(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (
                        property.Name == "__proto__"
                        || (
                            property.Name == "constructor"
                            && property.Value.ValueKind == JsonValueKind.Object
                            && property.Value.TryGetProperty("prototype", out _)
                        )
                        || HasForbiddenKey(property.Value)
                    )
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.Array:
                return element.EnumerateArray().Any(HasForbiddenKey);
            default:
                return false;
        }
    }

    // The Gateway's error schema: { error: { message: string, type?: string|null, code?: string|number|null, param?: unknown }, generationId?: string|null }.
    private static string? Validate(
        JsonElement? response,
        out JsonElement root,
        out JsonElement error,
        out JsonElement messageElement
    )
    {
        root = error = messageElement = default;
        if (response is not { ValueKind: JsonValueKind.Object } body)
        {
            return "Expected an object response";
        }

        root = body;
        if (!root.TryGetProperty("error", out error) || error.ValueKind != JsonValueKind.Object)
        {
            return "error: Required object";
        }

        if (!error.TryGetProperty("message", out messageElement) || messageElement.ValueKind != JsonValueKind.String)
        {
            return "error.message: Expected string";
        }

        if (!IsNullishString(error, "type"))
        {
            return "error.type: Expected string";
        }

        if (!IsNullishCode(error))
        {
            return "error.code: Expected string or number";
        }

        return IsNullishString(root, "generationId") ? null : "generationId: Expected string";
    }

    private static bool IsNullishString(JsonElement element, string name) =>
        !element.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.String or JsonValueKind.Null;

    private static bool IsNullishCode(JsonElement error) =>
        !error.TryGetProperty("code", out var value)
        || value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.Null;

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string>? Strings(JsonElement element, string name)
    {
        if (
            element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Array
            || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String)
        )
        {
            return null;
        }

        return value.EnumerateArray().Select(item => item.GetString()!).ToList();
    }
}
