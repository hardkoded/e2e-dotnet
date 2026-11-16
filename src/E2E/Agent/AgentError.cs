// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>Upstream's <c>agent/error.ts</c>: the class of every agent code, from the one table.</summary>
internal static class AgentError
{
    /// <summary>Exit and result class per agent code (upstream's <c>CATEGORY_BY_CODE</c>, derived from <c>AGENT_CODE_TABLE</c>).</summary>
    public static IReadOnlyDictionary<string, string> CategoryByCode { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AUTH_CREDENTIAL_UNAVAILABLE"] = "configuration",
        ["AUTH_CREDENTIAL_INVALID"] = "configuration",
        ["SECRET_UNAVAILABLE"] = "configuration",
        ["ENVIRONMENT_UNAVAILABLE"] = "infrastructure",
        ["APP_UNREACHABLE"] = "infrastructure",
        ["APP_ALREADY_RUNNING"] = "infrastructure",
        ["SEED_DATA_MISSING"] = "configuration",
        ["TEST_SETUP_FAILED"] = "configuration",
        ["APP_NOT_OPEN"] = "test",
        ["POLICY_DENIED"] = "configuration",
        ["MODEL_UNAVAILABLE"] = "configuration",
        ["AUTOMATION_UNSUPPORTED"] = "test",
        ["STEP_BUDGET_EXHAUSTED"] = "test",
        ["STEP_TIMEOUT"] = "test",
        ["CONTEXT_OVERFLOW"] = "test",
        ["MODEL_REFUSED"] = "test",
        ["MODEL_PROVIDER_FAILED"] = "infrastructure",
        ["CANCELLED"] = "infrastructure",
        ["AUTHENTICATION_FAILED"] = "test",
        ["MODEL_OUTPUT_INVALID"] = "test",
        ["LOCATOR_NOT_FOUND"] = "test",
        ["LOCATOR_AMBIGUOUS"] = "test",
        ["ACTION_FAILED"] = "test",
        ["STEP_NO_CONCLUSION"] = "test",
        ["ASSERTION_FAILED"] = "test",
        ["ASSERTION_INCONCLUSIVE"] = "test",
        ["REPLAY_STALE"] = "configuration",
    };

    /// <summary>
    /// Upstream's <c>toAgentError</c>: a failure on the closed agent code set. An agent error stays as it is. A runner or engine code the
    /// agent knows keeps it, <c>UNSUPPORTED_CAPABILITY</c> and <c>INVALID_CONFIG</c> are <c>POLICY_DENIED</c>, any other infrastructure
    /// failure is <c>APP_UNREACHABLE</c>, and the rest is <c>ACTION_FAILED</c>. A caller's cancel stays a cancellation (.NET-only).
    /// </summary>
    public static Exception ToAgentError(Exception cause)
    {
        ArgumentNullException.ThrowIfNull(cause);
        if (cause is AgentException or OperationCanceledException)
        {
            return cause;
        }

        var (category, code, message, _) = ErrorSerializer.Classify(cause);
        if (StepExecutors.AgentCodes.Contains(code))
        {
            return new AgentException(code, message, cause);
        }

        if (code is "UNSUPPORTED_CAPABILITY" or "INVALID_CONFIG")
        {
            return new AgentException("POLICY_DENIED", message, blocked: true, cause);
        }

        return category == "infrastructure"
            ? new AgentException("APP_UNREACHABLE", message, cause)
            : new AgentException("ACTION_FAILED", message, cause);
    }
}
