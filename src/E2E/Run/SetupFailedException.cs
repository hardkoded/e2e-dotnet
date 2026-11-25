// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// The setup test that saves a session a test restores did not pass (.NET-only: upstream's scheduler skips the dependent with
/// <see cref="Units.SetupFailedSkip"/> where the host here learns it while starting the dependent's session). The code is <c>SESSION_UNAVAILABLE</c>.
/// </summary>
internal sealed class SetupFailedException(string session, string setupTestId, string message)
    : E2EException("SESSION_UNAVAILABLE", message)
{
    /// <summary>The session the setup test was to save.</summary>
    public string Session { get; } = session;

    /// <summary>The id of the setup test that failed.</summary>
    public string SetupTestId { get; } = setupTestId;
}
