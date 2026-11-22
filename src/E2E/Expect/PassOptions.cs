// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>How <see cref="ValueExpectation{T}.ToPassAsync(PassOptions?, CancellationToken)"/> retries a block. Upstream's <c>PassOptions</c>.</summary>
public sealed class PassOptions
{
    /// <summary>
    /// Deadline for the block to pass. By default only the test's deadline applies, so it needs a cancellation token that can end (<c>INVALID_CONFIG</c>
    /// otherwise). The <c>toPass</c> config key sets another default. A negative value is <c>INVALID_CONFIG</c>.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Pauses between tries. The default is 100, 250, 500 and 1000 milliseconds, and the last pause repeats. Each is a positive whole number of
    /// milliseconds, and the list is not empty (<c>INVALID_CONFIG</c> otherwise).
    /// </summary>
    public IReadOnlyList<TimeSpan>? Intervals { get; init; }

    internal TimeProvider Clock { get; init; } = TimeProvider.System;
}
