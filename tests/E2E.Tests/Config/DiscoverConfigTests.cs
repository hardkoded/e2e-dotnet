// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Config;

public sealed class DiscoverConfigTests
{
    [Fact]
    public void Stops_at_the_repository_root_and_never_picks_a_config_above_it()
    {
        var root = Path.Combine(Path.GetTempPath(), "e2e-discover", Guid.NewGuid().ToString("n"));
        var cwd = Path.Combine(root, "repo", "a", "b");
        Directory.CreateDirectory(cwd);
        try
        {
            File.WriteAllText(Path.Combine(root, E2EConfig.FileName), "{}");
            // A worktree or submodule has a .git file, not a directory.
            File.WriteAllText(Path.Combine(root, "repo", ".git"), "");

            Assert.Null(E2EConfig.Find(cwd));

            var inside = Path.Combine(root, "repo", E2EConfig.FileName);
            File.WriteAllText(inside, "{}");
            Assert.Equal(inside, E2EConfig.Find(cwd));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
