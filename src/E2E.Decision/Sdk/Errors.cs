// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>InvalidArgumentError</c>: a call argument the SDK rejects before any request.</summary>
public sealed class InvalidArgumentException(string parameter, string message)
    : Exception("Invalid argument for parameter " + parameter + ": " + message)
{
    public string Parameter { get; } = parameter;
}

/// <summary>The AI SDK's <c>InvalidResponseDataError</c>: the provider's answers break the contract of the questions.</summary>
public sealed class InvalidResponseDataException(string message, object? answers = null) : Exception(message)
{
    /// <summary>The rejected answers.</summary>
    public object? Answers { get; } = answers;
}

/// <summary>The AI SDK's <c>JSONParseError</c>: text that is not valid JSON.</summary>
public sealed class JsonParseException(string text, Exception cause)
    : Exception("JSON parsing failed: Text: " + text + ".\nError message: " + cause.Message, cause)
{
    /// <summary>The text that failed to parse.</summary>
    public string Text { get; } = text;
}

/// <summary>The AI SDK's <c>LoadAPIKeyError</c>: the provider has no API key.</summary>
public sealed class LoadApiKeyException(string message) : Exception(message);

/// <summary>The AI SDK's <c>Experimental_DecisionRefusalError</c>: the model refused to answer some questions.</summary>
public sealed class DecisionRefusalException(IReadOnlyList<string> questionIds, string provider, string modelId)
    : Exception("The decision model " + provider + "/" + modelId + " refused to answer a question.")
{
    public IReadOnlyList<string> QuestionIds { get; } = questionIds;
}

/// <summary>The AI SDK's <c>Experimental_DecisionUnsupportedQuestionTypeError</c>: the model cannot answer a question's type.</summary>
public sealed class DecisionUnsupportedQuestionTypeException(
    string questionId,
    string questionType,
    string provider,
    string modelId
)
    : Exception(
        "The decision model "
            + provider
            + "/"
            + modelId
            + " does not support "
            + questionType
            + " questions (question \""
            + questionId
            + "\")."
    )
{
    public string QuestionId { get; } = questionId;
}

/// <summary>The AI SDK's <c>Experimental_ExtractionUnsupportedFieldTypeError</c>: the extraction model does not support the type of a requested field.</summary>
public sealed class ExtractionUnsupportedFieldTypeException(
    string fieldId,
    string fieldType,
    string provider,
    string modelId,
    string? message = null
)
    : Exception(
        message
            ?? "Field \""
                + fieldId
                + "\" has type \""
                + fieldType
                + "\", which is not supported by provider \""
                + provider
                + "\" and model \""
                + modelId
                + "\"."
    )
{
    public string FieldId { get; } = fieldId;

    public string FieldType { get; } = fieldType;

    public string Provider { get; } = provider;

    public string ModelId { get; } = modelId;
}

/// <summary>
/// The AI SDK's <c>APICallError</c>: a provider call that failed. <see cref="ExperimentalDecide.RunAsync"/> retries it when
/// <see cref="IsRetryable"/>, waiting for the provider's <c>retry-after-ms</c> or <c>retry-after</c> header when it is reasonable.
/// </summary>
public sealed class ApiCallException(
    string message,
    int? statusCode = null,
    IReadOnlyDictionary<string, string>? responseHeaders = null,
    bool? isRetryable = null,
    Exception? innerException = null
) : Exception(message, innerException)
{
    /// <summary>The HTTP status of the provider's answer; null when no answer came.</summary>
    public int? StatusCode { get; } = statusCode;

    /// <summary>The provider's response headers, lowercase names.</summary>
    public IReadOnlyDictionary<string, string>? ResponseHeaders { get; } = responseHeaders;

    /// <summary>The body of the provider's answer, when it sent one.</summary>
    public string? ResponseBody { get; init; }

    /// <summary>The body the provider SDK already parsed, when it did (upstream <c>APICallError.data</c>; <c>Exception.Data</c> is taken). Null means there is none.</summary>
    public JsonElement? ParsedData { get; init; }

    /// <summary>Whether the call may be sent again. Upstream's default: no answer at all, or a 408, 409, 429, or 5xx.</summary>
    public bool IsRetryable { get; } = isRetryable ?? (statusCode is null or 408 or 409 or 429 or >= 500);
}

/// <summary>The AI SDK's <c>DownloadError</c> (<c>@ai-sdk/provider-utils</c>): a file the SDK could not download.</summary>
public sealed class DownloadException : Exception
{
    /// <summary>Creates the error. Without <paramref name="message"/>, it reads as upstream's: the status, or the inner error.</summary>
    public DownloadException(
        string url,
        int? statusCode = null,
        string? statusText = null,
        string? message = null,
        Exception? innerException = null
    )
        : base(
            message
                ?? (
                    innerException is null
                        ? "Failed to download " + url + ": " + statusCode + " " + statusText
                        : "Failed to download " + url + ": " + innerException.Message
                ),
            innerException
        )
    {
        Url = url;
        StatusCode = statusCode;
        StatusText = statusText;
    }

    /// <summary>The URL that failed.</summary>
    public string Url { get; }

    /// <summary>The HTTP status of the answer; null when none came.</summary>
    public int? StatusCode { get; }

    /// <summary>The reason phrase of the answer.</summary>
    public string? StatusText { get; }
}

/// <summary>The AI SDK's <c>UnsupportedFunctionalityError</c>: the provider cannot take an input, such as a file of an unsupported media type.</summary>
public sealed class UnsupportedFunctionalityException(string functionality, string? message = null)
    : Exception(message ?? "'" + functionality + "' functionality not supported.")
{
    public string Functionality { get; } = functionality;
}

/// <summary>The AI SDK's <c>NoSuchModelError</c>: no model of that type has that id.</summary>
public class NoSuchModelException(string modelId, string modelType, string? message = null)
    : Exception(message ?? "No such " + modelType + ": " + modelId)
{
    public string ModelId { get; } = modelId;

    /// <summary>The kind of model, such as <c>decisionModel</c>.</summary>
    public string ModelType { get; } = modelType;
}

/// <summary>The AI SDK's <c>NoSuchProviderError</c>: the registry has no provider with that id.</summary>
public sealed class NoSuchProviderException(
    string modelId,
    string modelType,
    string providerId,
    IReadOnlyList<string> availableProviders
)
    : NoSuchModelException(
        modelId,
        modelType,
        "No such provider: " + providerId + " (available providers: " + string.Join(",", availableProviders) + ")"
    )
{
    public string ProviderId { get; } = providerId;

    public IReadOnlyList<string> AvailableProviders { get; } = availableProviders;
}
