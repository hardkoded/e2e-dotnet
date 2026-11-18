// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

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
