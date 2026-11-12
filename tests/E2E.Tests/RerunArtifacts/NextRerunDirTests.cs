// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RerunArtifacts;

/// <summary>Upstream's <c>describe('nextRerunDir')</c> (<c>tests/unit/rerun-artifacts.test.ts</c>, tester-army/e2e <c>50fa7af</c>).</summary>
public sealed class NextRerunDirTests
{
    [Fact]
    public async Task Names_rerun_1_in_a_tree_without_one_then_the_number_past_the_highest_any_tests_directory_holds()
    {
        using var tree = new RerunTree();
        tree.Plant("a-1111/attempt-1/x.png");
        Assert.Equal("rerun-1", await ArtifactPaths.NextRerunDirAsync(tree.Root));
        tree.Plant("a-1111/rerun-1/attempt-1/x.png");
        tree.Plant("b-2222/rerun-3/attempt-1/x.png");
        Assert.Equal("rerun-4", await ArtifactPaths.NextRerunDirAsync(tree.Root));

        // Names that only look alike do not count.
        tree.Plant("c-3333/rerun-07/x.png");
        tree.Plant("c-3333/rerun-x/x.png");
        Assert.Equal("rerun-4", await ArtifactPaths.NextRerunDirAsync(tree.Root));
    }

    [Fact]
    public async Task Names_rerun_1_when_the_tree_is_not_there()
    {
        using var tree = new RerunTree();
        Assert.Equal("rerun-1", await ArtifactPaths.NextRerunDirAsync(tree.Root));
    }
}
