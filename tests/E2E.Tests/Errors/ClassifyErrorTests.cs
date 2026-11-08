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
    public void Appends_the_cause_chain_to_a_plain_error_the_way_fetch_reports_a_refused_connection()
    {
        var refused = new Exception("connect ECONNREFUSED 127.0.0.1:3000") { Data = { ["code"] = "ECONNREFUSED" } };
        Assert.Equal("fetch failed: connect ECONNREFUSED 127.0.0.1:3000", ErrorSerializer.Classify(new InvalidOperationException("fetch failed", refused)).Message);
        // Node reports a dual-stack failure as an aggregate whose members carry the detail.
        var aggregate = new AggregateException(
            "",
            new Exception("") { Data = { ["code"] = "ECONNREFUSED" } },
            new Exception("connect ECONNREFUSED ::1:3000"));
        Assert.Equal("fetch failed: ECONNREFUSED; connect ECONNREFUSED ::1:3000", ErrorSerializer.Classify(new InvalidOperationException("fetch failed", aggregate)).Message);
        Assert.Equal("same", ErrorSerializer.MessageWithCauses(new Exception("same", new Exception("same"))));
        // Upstream's non-Error cause (a string) is an Exception that carries the text here: an InnerException is always an Exception.
        Assert.Equal("outer: text", ErrorSerializer.MessageWithCauses(new Exception("outer", new Exception("text"))));
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

    [Fact]
    public void Keeps_an_infrastructure_error_the_runner_named_as_it_is()
    {
        // .NET-only: upstream throws these as InfrastructureError, which asEngineError (the closed engine code set) never matches.
        // The port's EngineException is both, so a code outside the set and a Mapped error must stay as they are.
        var unreachable = ErrorSerializer.Classify(new EngineException("APP_UNREACHABLE", "app down"));
        Assert.Equal(("infrastructure", "APP_UNREACHABLE", "app down"), (unreachable.Category, unreachable.Code, unreachable.Message));
        var mapped = ErrorSerializer.Classify(new EngineException(EngineErrorCodes.InvalidState, "kept") { Mapped = true });
        Assert.Equal(("infrastructure", "INVALID_STATE"), (mapped.Category, mapped.Code));
    }

    [Fact]
    public void Maps_a_stale_node_and_a_not_actionable_node_without_a_retry_claim()
    {
        // .NET-only: upstream's translateEngineError cases, reached through classifyError (the port had them in TranslateEngineError only).
        var stale = ErrorSerializer.Classify(new EngineException(EngineErrorCodes.NodeStale, "gone", retryable: true));
        Assert.Equal(("test", "LOCATOR_NOT_FOUND", false), (stale.Category, stale.Code, stale.Retryable));
        var notActionable = ErrorSerializer.Classify(new EngineException(EngineErrorCodes.NotActionable, "covered"));
        Assert.Equal(("test", "ACTION_FAILED"), (notActionable.Category, notActionable.Code));
    }
}
