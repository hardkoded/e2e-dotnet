// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Filters = E2E.TestFilters;

namespace E2E.Tests.TestFilters;

/// <summary>Upstream's <c>describe('what a filter is told')</c> (<c>tests/integration/test-filters.test.ts</c>).</summary>
public sealed class WhatAFilterIsToldTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-filters-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>
    /// Partial: the port has no runner or <c>e2e list</c>, so <c>RepoConfigFile</c>, which the runner calls with the loaded config, is called
    /// with each config the upstream test loads; a config object has no port, so a config outside the repository stands in for it.
    /// </summary>
    [Fact]
    public void Partial_names_the_config_file_the_run_loaded_whatever_its_name_relative_to_the_repository_root_and_none_for_a_config_outside_it()
    {
        var root = Paths.OnDiskPath(_dir);
        Directory.CreateDirectory(Path.Combine(root, "apps", "web"));
        foreach (var file in new[] { "e2e.config.json", "e2e.ci.config.json", Path.Combine("apps", "web", "e2e.ci.config.json") })
        {
            File.WriteAllText(Path.Combine(root, file), "{}\n");
        }

        Assert.Equal("e2e.config.json", Filters.RepoConfigFile(Path.Combine(root, "e2e.config.json"), root));
        Assert.Equal("e2e.ci.config.json", Filters.RepoConfigFile(Path.Combine(root, "e2e.ci.config.json"), root));
        Assert.Equal("apps/web/e2e.ci.config.json", Filters.RepoConfigFile(Path.Combine(root, "apps", "web", "e2e.ci.config.json"), root));

        // Git lists a symlinked config under the link's own path.
        try
        {
            File.CreateSymbolicLink(Path.Combine(root, "e2e.link.config.json"), "e2e.ci.config.json");
            Assert.Equal("e2e.link.config.json", Filters.RepoConfigFile(Path.Combine(root, "e2e.link.config.json"), root));
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            // Windows without the privilege to link.
        }

        // A config outside the repository can name no changed file.
        var elsewhere = Directory.CreateTempSubdirectory("e2e-filters-outside-").FullName;
        try
        {
            Assert.Null(Filters.RepoConfigFile(Path.Combine(elsewhere, "e2e.config.json"), root));
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }
}
