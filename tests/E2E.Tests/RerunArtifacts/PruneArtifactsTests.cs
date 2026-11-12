// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RerunArtifacts;

/// <summary>Upstream's <c>describe('pruneArtifacts')</c> (<c>tests/unit/rerun-artifacts.test.ts</c>, tester-army/e2e <c>50fa7af</c>).</summary>
public sealed class PruneArtifactsTests
{
    [Fact]
    public async Task Keeps_exactly_the_files_the_report_names_untouched_and_removes_the_rest_and_the_directories_left_empty()
    {
        using var tree = new RerunTree();
        string[] kept = ["web/a/default/attempt-0/screenshots/001-landing.png", "web/b/default/attempt-0/video/video.webm", "rerun-1/web/b/default/attempt-0/failure/screen.txt"];
        string[] dropped = ["web/a/default/attempt-0/stray.log", "web/c/default/attempt-0/video/video.webm", "web/sessions/0192/screen.png", "top-level.txt"];
        foreach (var file in kept.Concat(dropped))
        {
            tree.Plant(file);
        }

        await ArtifactPaths.PruneArtifactsAsync(tree.Root, new HashSet<string>(kept));

        Assert.Equal(kept.Order(StringComparer.Ordinal), tree.Files());
        foreach (var file in kept)
        {
            Assert.Equal(file, await File.ReadAllTextAsync(Path.Combine(tree.Root, Path.Combine(file.Split('/')))));
        }

        Assert.False(Directory.Exists(Path.Combine(tree.Root, "web", "c")));
        Assert.False(Directory.Exists(Path.Combine(tree.Root, "web", "sessions")));
    }

    [Fact]
    public async Task Empties_the_tree_when_the_report_names_nothing_and_accepts_a_tree_that_is_not_there()
    {
        using var tree = new RerunTree();
        tree.Plant("web/a/default/attempt-0/screenshots/001.png");
        await ArtifactPaths.PruneArtifactsAsync(tree.Root, new HashSet<string>());
        Assert.Empty(tree.Files());
        Directory.Delete(tree.Root, recursive: true);
        await ArtifactPaths.PruneArtifactsAsync(tree.Root, new HashSet<string> { "web/a.png" });
    }
}
