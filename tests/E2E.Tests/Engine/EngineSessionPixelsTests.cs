// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentFallbackHandoff;

namespace E2E.Tests.Engine;

/// <summary>
/// .NET-only: the port's engine-to-session adapter (<c>EngineSession</c>) decides the pixels of an observation once, as upstream's
/// <c>createEngineSession</c> does in <c>observe</c> (<c>MASKING_UNPROVEN</c>), over the engines that return an <see cref="Observation"/>.
/// Upstream's own cases are in <see cref="CreateEngineSessionPixelsOnlyObservationTests"/>. The test over a reported secure-node count has no
/// upstream counterpart: upstream counts the tree.
/// </summary>
public sealed class EngineSessionPixelsTests
{
    private static readonly SemanticNode Password = new()
    {
        Ref = "password",
        Role = "textbox",
        Name = "Password",
        States = new NodeStates { Secure = true },
    };

    private static readonly ObserveOptions Asked = new() { Pixels = true };

    private static async Task<Observation> LookAsync(
        Observation answer,
        ObserveOptions? options,
        int? secureNodeCount = null
    )
    {
        var engine = new FallbackEngine
        {
            Observe = (_, _, _) =>
                Task.FromResult(
                    new Observation
                    {
                        Route = answer.Route,
                        Roots = answer.Roots,
                        Pixels = answer.Pixels,
                        MaskedRegionCount = answer.MaskedRegionCount,
                        TreeUnavailable = answer.TreeUnavailable,
                        SecureNodeCount = secureNodeCount,
                    }
                ),
        };
        var session = EngineSession.Create(
            await engine.StartAsync(new EngineStartOptions(), CancellationToken.None),
            "run",
            "attempt"
        );
        return await session.ObserveAsync(
            options,
            TimeSpan.FromSeconds(5),
            OperationContext.Test,
            CancellationToken.None
        );
    }

    private static Observation Screen(params SemanticNode[] nodes) => new() { Route = "/", Roots = nodes };

    private static Observation WithPixels(int? masked, params SemanticNode[] nodes) =>
        new()
        {
            Route = "/",
            Roots = nodes,
            Pixels = new EngineScreenshot(ScreenshotShot(), 1, masked),
        };

    private static byte[] ScreenshotShot() => FallbackFixtures.Shot(1);

    [Fact]
    public async Task Keeps_pixels_whose_masked_regions_cover_every_secure_node()
    {
        var look = await LookAsync(WithPixels(1, Password), Asked);
        Assert.NotNull(look.Pixels);
        Assert.Null(look.PixelsWithheld);
    }

    [Fact]
    public async Task Withholds_pixels_the_engine_masked_fewer_regions_of_than_the_screen_has_secure_nodes_and_keeps_the_tree()
    {
        var look = await LookAsync(WithPixels(0, Password), Asked);
        Assert.Null(look.Pixels);
        Assert.Equal(PixelsWithheld.MaskingUnproven, look.PixelsWithheld);
        Assert.Single(look.Roots);

        // An engine that cannot count the regions it masked has masked none.
        Assert.Equal(
            PixelsWithheld.MaskingUnproven,
            (await LookAsync(WithPixels(null, Password), Asked)).PixelsWithheld
        );
    }

    [Fact]
    public async Task Withholds_a_capture_the_engine_answered_with_a_count_and_no_pixels()
    {
        var look = await LookAsync(
            new Observation
            {
                Route = "/",
                Roots = [Password],
                MaskedRegionCount = 0,
            },
            Asked
        );
        Assert.Null(look.Pixels);
        Assert.Equal(PixelsWithheld.MaskingUnproven, look.PixelsWithheld);
    }

    [Fact]
    public async Task Marks_nothing_withheld_for_a_screen_with_no_secure_node_or_a_capture_nobody_asked_for()
    {
        Assert.Null((await LookAsync(WithPixels(null), Asked)).PixelsWithheld);
        Assert.NotNull((await LookAsync(WithPixels(null), Asked)).Pixels);

        // The engine took no screenshot and reported no count: no screenshot, and nothing withheld.
        var untaken = await LookAsync(Screen(Password), Asked);
        Assert.Null(untaken.Pixels);
        Assert.Null(untaken.PixelsWithheld);

        // Pixels nobody asked for are left as the engine answered.
        var unasked = await LookAsync(WithPixels(0, Password), null);
        Assert.NotNull(unasked.Pixels);
        Assert.Null(unasked.PixelsWithheld);
    }

    [Fact]
    public async Task Counts_the_larger_of_the_secure_nodes_the_engine_reports_and_the_tree_lists()
    {
        Assert.Equal(
            PixelsWithheld.MaskingUnproven,
            (await LookAsync(WithPixels(1, Password), Asked, secureNodeCount: 2)).PixelsWithheld
        );
        Assert.Equal(
            PixelsWithheld.MaskingUnproven,
            (await LookAsync(WithPixels(0, Password), Asked, secureNodeCount: 0)).PixelsWithheld
        );
    }

    [Fact]
    public async Task Leaves_the_fallback_pixels_of_a_screen_with_no_tree_on_the_engines_own_proof()
    {
        var look = await LookAsync(
            new Observation
            {
                Route = "/",
                Roots = [],
                TreeUnavailable = true,
                Pixels = new EngineScreenshot(ScreenshotShot(), 1, 0),
            },
            new ObserveOptions { Pixels = true, PixelFallback = true },
            secureNodeCount: 1
        );
        Assert.NotNull(look.Pixels);
        Assert.Null(look.PixelsWithheld);
    }
}
