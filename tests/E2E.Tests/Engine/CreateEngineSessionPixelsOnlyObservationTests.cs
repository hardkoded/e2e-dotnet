// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Reports;
using E2E.Tests.Helpers;

namespace E2E.Tests.Engine;

/// <summary>Upstream's <c>describe('createEngineSession pixels-only observation')</c> (<c>engine.test.ts</c>).</summary>
public sealed class CreateEngineSessionPixelsOnlyObservationTests
{
    private static ObservationPixels Pixels() => new(new byte[8], "image/png", 2, 2, 1);

    [Fact]
    public async Task Requires_explicit_fallback_permission_and_discards_stale_semantic_content()
    {
        EngineSnapshot raw = new(new EngineNode(new NodeRef("root", "")), Snapshot.Viewport)
        {
            TreeUnavailable = true,
            Pixels = Pixels(),
        };
        var received = new List<ObserveOptions?>();
        var session = EngineSessions.CreateEngineSession(
            new EngineSessionOptions
            {
                Engine = EngineManifest.DefineEngine(
                    new EngineSpec
                    {
                        Name = "toy",
                        Version = "1.0.0",
                        SpiVersion = 1,
                        Observe = (_, options) =>
                        {
                            received.Add(options);
                            return Task.FromResult(raw);
                        },
                    }
                ),
                TargetName = "toy",
            }
        );

        var refused = await Assert.ThrowsAsync<TestException>(() => session.ObserveAsync(Snapshot.Operation));
        Assert.Equal("UNSUPPORTED_CAPABILITY", refused.Code);

        var captured = await session.ObserveAsync(Snapshot.Operation, new ObserveOptions { PixelFallback = true });
        Assert.Equal(new ObserveOptions { PixelFallback = true }, received[^1]);
        Assert.Equal("pixels", captured.Kind);
        Assert.Null(captured.Tree);
        Assert.Equal(captured.Revision, captured.Root.Revision);

        raw = new EngineSnapshot(
            new EngineNode(new NodeRef("root", ""), Children: [Snapshot.Node("stale", "Old control")]),
            Snapshot.Viewport
        )
        {
            TreeUnavailable = true,
            Pixels = Pixels(),
        };
        var stale = await Assert.ThrowsAsync<EngineException>(() =>
            session.ObserveAsync(Snapshot.Operation, new ObserveOptions { PixelFallback = true })
        );
        Assert.Equal("INVALID_STATE", stale.Code);

        raw = new EngineSnapshot(new EngineNode(new NodeRef("root", "")), Snapshot.Viewport) { TreeUnavailable = true };
        var noPixels = await Assert.ThrowsAsync<TestException>(() =>
            session.ObserveAsync(Snapshot.Operation, new ObserveOptions { PixelFallback = true })
        );
        Assert.Equal("UNSUPPORTED_CAPABILITY", noPixels.Code);
    }

    [Fact]
    public async Task Accepts_a_snapshot_with_no_nodes_and_pixels_a_vision_only_body_is_observable()
    {
        var pixels = new ObservationPixels(new byte[8], "image/png", 4, 2, 1);
        var session = EngineSessions.CreateEngineSession(
            new EngineSessionOptions
            {
                Engine = EngineManifest.DefineEngine(
                    new EngineSpec
                    {
                        Name = "vision",
                        Version = "1.0.0",
                        SpiVersion = 1,
                        Observe = (_, _) => Task.FromResult(Snapshot.Of([], pixels: pixels, maskedRegionCount: 0)),
                    }
                ),
                TargetName = "desktop",
            }
        );

        var observation = await session.ObserveAsync(Snapshot.Operation, new ObserveOptions { Pixels = true });

        Assert.Equal("semantic", observation.Kind);
        Assert.Equal("root", observation.Tree!.Role);
        Assert.Empty(observation.Tree.Children ?? []);
        Assert.Same(pixels, observation.Pixels);
        Assert.Equal(new ObservationRedaction(0, 0), observation.Redaction);
    }

    [Fact]
    public async Task Keeps_pixels_only_when_the_masked_regions_cover_every_secure_node_and_marks_a_screenshot_it_cannot_keep_as_withheld()
    {
        var pixels = new ObservationPixels(new byte[8], "image/png", 4, 2, 1);
        var pin = new EngineNode(
            new NodeRef("pin", ""),
            Role: "textbox",
            Name: "PIN",
            States: new EngineNodeStates(Secure: true)
        );
        var label = new EngineNode(new NodeRef("label", ""), Role: "text", Name: "Sign in");
        var raw = Snapshot.Of([pin, label]);
        var session = EngineSessions.CreateEngineSession(
            new EngineSessionOptions
            {
                Engine = EngineManifest.DefineEngine(
                    new EngineSpec
                    {
                        Name = "toy",
                        Version = "1.0.0",
                        SpiVersion = 1,
                        Observe = (_, _) => Task.FromResult(raw),
                    }
                ),
                TargetName = "toy",
            }
        );

        async Task<(ObservationPixels? Pixels, PixelsWithheld? Withheld)> Look(EngineSnapshot next)
        {
            raw = next;
            var observation = await session.ObserveAsync(Snapshot.Operation, new ObserveOptions { Pixels = true });
            Assert.Equal("semantic", observation.Kind);
            return (observation.Pixels, observation.PixelsWithheld);
        }

        Assert.Equal((pixels, null), await Look(Snapshot.Of([pin, label], pixels: pixels, maskedRegionCount: 1)));

        // Sent without proof, or withheld by the engine: either way the session keeps no image.
        Assert.Equal((null, PixelsWithheld.MaskingUnproven), await Look(Snapshot.Of([pin, label], pixels: pixels)));
        Assert.Equal(
            (null, PixelsWithheld.MaskingUnproven),
            await Look(Snapshot.Of([pin, label], maskedRegionCount: 0))
        );

        // No screenshot taken, or one withheld on a screen with nothing to mask, withholds nothing.
        Assert.Equal((null, null), await Look(Snapshot.Of([pin, label])));
        Assert.Equal((null, null), await Look(Snapshot.Of([label], maskedRegionCount: 0)));

        // A count no pixels request asked for withholds nothing either.
        raw = Snapshot.Of([pin, label], maskedRegionCount: 0);
        var unasked = await session.ObserveAsync(Snapshot.Operation);
        Assert.Equal("semantic", unasked.Kind);
        Assert.Null(unasked.PixelsWithheld);
    }
}
