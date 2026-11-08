// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Errors;

/// <summary>Upstream's <c>describe('classifyError')</c> (<c>tests/unit/errors.test.ts</c>).</summary>
public sealed class ClassifyErrorTests
{
    [Fact]
    public void Partial_Wraps_unknown_errors_as_test_failures()
    {
        // PARTIAL: C# can only throw an Exception, so upstream's second check (a thrown string) has no counterpart.
        Assert.Equal("test", ErrorSerializer.Classify(new Exception("nope")).Category);
    }

    [Fact]
    public void Partial_Appends_the_cause_chain_to_a_plain_error_the_way_fetch_reports_a_refused_connection()
    {
        // PARTIAL: the port follows InnerException only, so upstream's AggregateError check (members carrying the detail) and its
        // non-Error cause check ("outer: text") have no counterpart. Tracked in #1208.
        var refused = new Exception("connect ECONNREFUSED 127.0.0.1:3000");
        Assert.Equal("fetch failed: connect ECONNREFUSED 127.0.0.1:3000", ErrorSerializer.Classify(new InvalidOperationException("fetch failed", refused)).Message);
        Assert.Equal("same", ErrorSerializer.MessageWithCauses(new Exception("same", new Exception("same"))));
        var deep = new Exception("a", new Exception("b", new Exception("c", new Exception("d", new Exception("e")))));
        Assert.Equal("a: b: c: d", ErrorSerializer.MessageWithCauses(deep));
    }

    [Fact]
    public void Applies_the_canonical_driver_mapping_to_EngineErrors_from_any_surface()
    {
        var failure = ErrorSerializer.Classify(new EngineException(EngineErrorCodes.EngineFailure, "engine died"));
        Assert.Equal("infrastructure", failure.Category);
        Assert.Equal("ENGINE_FAILURE", failure.Code);

        var unsupported = ErrorSerializer.Classify(new EngineException(EngineErrorCodes.UnsupportedCapability, "no video"));
        Assert.Equal("configuration", unsupported.Category);
        Assert.Equal("UNSUPPORTED_CAPABILITY", unsupported.Code);

        var cancelled = ErrorSerializer.Classify(new EngineException(EngineErrorCodes.Cancelled, "stop"));
        Assert.Equal("infrastructure", cancelled.Category);
        Assert.Equal("CANCELLED", cancelled.Code);

        var invalidState = ErrorSerializer.Classify(new EngineException(EngineErrorCodes.InvalidState, "nothing open"));
        Assert.Equal("test", invalidState.Category);
        Assert.Equal("APP_NOT_OPEN", invalidState.Code);
    }
}
