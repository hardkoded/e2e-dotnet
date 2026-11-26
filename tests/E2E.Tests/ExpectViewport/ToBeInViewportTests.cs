// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentFallbackHandoff;

namespace E2E.Tests.ExpectViewport;

/// <summary>
/// Upstream's <c>expect-viewport.test.ts</c>, <c>describe('toBeInViewport')</c>. A screen measuring through the session adapter, as
/// upstream's <c>screenOver</c> does: the engine's answer is held to the contract before the matcher reads it. Not ported: the unknown
/// option and the non-boolean <c>intersects</c> rows, which C# types out (the matcher has no options bag, and a record's <c>Intersects</c>
/// is a <c>bool</c>); a <c>ratio</c> that is NaN is the one invalid number C# can still pass.
/// </summary>
public sealed class ToBeInViewportTests
{
    private static readonly TimeSpan Soon = TimeSpan.FromMilliseconds(50);

    private static readonly SemanticNode Target = new()
    {
        Ref = "target",
        Role = "generic",
        TestId = "target",
    };

    /// <summary>A screen whose <c>target</c> node is present while <paramref name="present"/> says so, measured by <paramref name="measure"/>.</summary>
    private static async Task<Screen> ScreenMeasuringAsync(
        Func<SemanticNode, E2E.Engine.ViewportIntersection> measure,
        Func<bool>? present = null,
        SemanticNode? node = null
    )
    {
        present ??= () => true;
        node ??= Target;
        var session = EngineSession.Create(
            await new FallbackEngine { ViewportIntersection = measure }.StartAsync(
                new EngineStartOptions(),
                CancellationToken.None
            ),
            "run",
            "attempt",
            "fixture"
        );
        var engine = new E2E.LocatorEngine(
            new LocatorEngineOptions
            {
                Observe = _ => Task.FromResult(new Observation { Route = "/", Roots = present() ? [node] : [] }),
                ViewportIntersection = session.MeasuresViewport ? session.ViewportIntersectionAsync : null,
                Perform = (_, _, _, _) => Task.CompletedTask,
                Cancellation = () => CancellationToken.None,
                ActionTimeout = TimeSpan.FromMilliseconds(300),
                AssertionTimeout = TimeSpan.FromMilliseconds(300),
            }
        );
        return new Screen(engine, () => { });
    }

    private static async Task<E2EException> FailureAsync(Func<Task> run) =>
        await Assert.ThrowsAnyAsync<E2EException>(run);

    [Fact]
    public async Task Passes_on_any_area_inside_by_default_and_compares_the_share_with_ratio()
    {
        var locator = (await ScreenMeasuringAsync(_ => new E2E.Engine.ViewportIntersection(true, 0.1))).GetByTestId(
            "target"
        );

        await Expect.That(locator).ToBeInViewportAsync();
        await Expect.That(locator).ToBeInViewportAsync(ratio: 0);
        await Expect.That(locator).ToBeInViewportAsync(ratio: 0.1);
        var error = await FailureAsync(() => Expect.That(locator).ToBeInViewportAsync(ratio: 0.11, timeout: Soon));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Matches(
            @"expected: at least 0\.11 of its box inside the viewport\nobserved: 0\.1 of its box",
            error.Message
        );
    }

    [Fact]
    public async Task Reads_no_match_and_a_box_outside_as_outside_the_viewport()
    {
        var absent = (
            await ScreenMeasuringAsync(_ => new E2E.Engine.ViewportIntersection(true, 1), () => false)
        ).GetByTestId("target");
        await Expect.That(absent).Not.ToBeInViewportAsync(timeout: Soon);
        var error = await FailureAsync(() => Expect.That(absent).ToBeInViewportAsync(timeout: Soon));
        Assert.Contains("observed: no node", error.Message, StringComparison.Ordinal);

        var outside = (await ScreenMeasuringAsync(_ => new E2E.Engine.ViewportIntersection(false, 0))).GetByTestId(
            "target"
        );
        await Expect.That(outside).Not.ToBeInViewportAsync(timeout: Soon);
    }

    [Fact]
    public async Task Polls_until_the_node_enters_the_viewport()
    {
        var inside = false;
        _ = Task.Delay(25).ContinueWith(_ => inside = true, TaskScheduler.Default);
        var screen = await ScreenMeasuringAsync(_ =>
            inside ? new E2E.Engine.ViewportIntersection(true, 1) : new E2E.Engine.ViewportIntersection(false, 0)
        );

        await Expect.That(screen.GetByTestId("target")).ToBeInViewportAsync(timeout: TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public async Task Refuses_ratio_on_an_engine_that_measures_only_the_visible_part_of_a_box_even_under_not()
    {
        var visiblePart = (await ScreenMeasuringAsync(_ => new E2E.Engine.ViewportIntersection(true))).GetByTestId(
            "target"
        );
        await Expect.That(visiblePart).ToBeInViewportAsync();
        // 0 asks for any part, which needs no share.
        await Expect.That(visiblePart).ToBeInViewportAsync(ratio: 0);
        foreach (var negated in new[] { false, true })
        {
            var expectation = negated ? Expect.That(visiblePart).Not : Expect.That(visiblePart);
            var error = await FailureAsync(() => expectation.ToBeInViewportAsync(ratio: 0.5));
            Assert.Equal("UNSUPPORTED_CAPABILITY", error.Code);
            Assert.Contains("drop ratio", error.Message, StringComparison.Ordinal);
        }

        // Nothing of the node is on screen, so no share is needed to answer.
        await Expect
            .That((await ScreenMeasuringAsync(_ => new E2E.Engine.ViewportIntersection(false))).GetByTestId("target"))
            .Not.ToBeInViewportAsync(ratio: 0.5);
    }

    [Fact]
    public async Task Fails_at_once_when_the_engine_cannot_measure_the_node_even_under_not()
    {
        var unmeasurable = (
            await ScreenMeasuringAsync(_ =>
                throw new EngineException(
                    EngineErrorCodes.UnsupportedCapability,
                    "node target has no bounds on the screen",
                    retryable: false
                )
            )
        ).GetByTestId("target");
        var first = await FailureAsync(() => Expect.That(unmeasurable).Not.ToBeInViewportAsync());
        Assert.Equal("UNSUPPORTED_CAPABILITY", first.Code);

        // Refused before the resolve: a missing match must not let `.not` pass on an engine that cannot measure.
        foreach (var nodes in new[] { new[] { Target }, Array.Empty<SemanticNode>() })
        {
            var noCapability = ScreenFixture.Create(nodes).GetByTestId("target");
            var error = await FailureAsync(() => Expect.That(noCapability).Not.ToBeInViewportAsync());
            Assert.Equal("UNSUPPORTED_CAPABILITY", error.Code);
            Assert.Contains("does not implement viewportIntersection", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Samples_a_node_that_went_stale_before_it_was_measured_again_instead_of_reading_it_as_gone()
    {
        var measures = 0;
        var flaky = (
            await ScreenMeasuringAsync(_ =>
                Interlocked.Increment(ref measures) == 1
                    ? throw new EngineException(EngineErrorCodes.NodeStale, "navigation changed", retryable: true)
                    : new E2E.Engine.ViewportIntersection(true, 1)
            )
        ).GetByTestId("target");
        await Expect.That(flaky).ToBeInViewportAsync(timeout: TimeSpan.FromMilliseconds(200));
        Assert.Equal(2, measures);

        var stale = (
            await ScreenMeasuringAsync(_ =>
                throw new EngineException(EngineErrorCodes.NodeStale, "navigation changed", retryable: true)
            )
        ).GetByTestId("target");
        var error = await FailureAsync(() => Expect.That(stale).Not.ToBeInViewportAsync(timeout: Soon));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Contains(
            "observed: a node that went stale before it was measured",
            error.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Measures_a_secure_field_without_reading_or_printing_its_value()
    {
        var secure = new SemanticNode
        {
            Ref = Target.Ref,
            TestId = Target.TestId,
            Role = "textbox",
            Value = "hunter2",
            States = new NodeStates { Secure = true },
        };
        var locator = (
            await ScreenMeasuringAsync(_ => new E2E.Engine.ViewportIntersection(false, 0), () => true, secure)
        ).GetByTestId("target");

        await Expect.That(locator).Not.ToBeInViewportAsync();
        var error = await FailureAsync(() => Expect.That(locator).ToBeInViewportAsync(timeout: Soon));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.DoesNotContain("hunter2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_on_an_engine_answer_outside_the_contract_or_one_that_contradicts_itself()
    {
        foreach (
            var answer in new[]
            {
                new E2E.Engine.ViewportIntersection(true, 2),
                new E2E.Engine.ViewportIntersection(false, -0.1),
                new E2E.Engine.ViewportIntersection(false, 1),
                new E2E.Engine.ViewportIntersection(true, 0),
            }
        )
        {
            var locator = (await ScreenMeasuringAsync(_ => answer)).GetByTestId("target");
            var error = await FailureAsync(() => Expect.That(locator).Not.ToBeInViewportAsync(ratio: 1));
            Assert.Equal("ENGINE_FAILURE", error.Code);
        }
    }

    [Fact]
    public async Task Prints_a_share_that_falls_short_of_ratio_in_full_when_rounding_would_show_it_as_met()
    {
        var locator = (await ScreenMeasuringAsync(_ => new E2E.Engine.ViewportIntersection(true, 0.99975))).GetByTestId(
            "target"
        );
        var error = await FailureAsync(() => Expect.That(locator).ToBeInViewportAsync(ratio: 1, timeout: Soon));
        Assert.Contains("observed: 0.99975 of its box inside the viewport", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Partial_Rejects_an_invalid_ratio_and_unknown_options_before_reading()
    {
        var locator = Expect.That(ScreenFixture.Create(Target).GetByTestId("target"));

        foreach (var ratio in new[] { -0.1, 1.1, double.NaN })
        {
            var error = Assert.Throws<TestException>(() =>
            {
                _ = locator.ToBeInViewportAsync(ratio: ratio);
            });
            Assert.Equal("INVALID_ARGUMENT", error.Code);
        }
    }
}
