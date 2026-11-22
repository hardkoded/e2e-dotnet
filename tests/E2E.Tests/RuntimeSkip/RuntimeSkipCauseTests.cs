// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.RuntimeSkip;

/// <summary>Upstream's <c>describe('runtimeSkipCause')</c> of <c>runtime-skip.test.ts</c>.</summary>
public sealed class RuntimeSkipCauseTests
{
    /// <summary>Partial: upstream builds a skip from another copy of the package by its brand, with <c>isRuntimeSkip</c>; a .NET host ends the test with its own skip and no value crosses copies, so there is no brand to recognize. The part checked is that a skip naming no cause reports <c>explicit</c>.</summary>
    [Fact]
    public void Partial_reports_explicit_for_a_runtime_skip_from_a_copy_of_e2e_that_names_no_cause() =>
        Assert.Equal("explicit", Skip.RuntimeSkipCause(null));

    [Fact]
    public void Keeps_fixme_and_reads_any_other_value_as_explicit()
    {
        Assert.Equal("fixme", Skip.RuntimeSkipCause("fixme"));
        Assert.Equal("explicit", Skip.RuntimeSkipCause("explicit"));
        Assert.Equal("explicit", Skip.RuntimeSkipCause("skipped"));
    }
}
