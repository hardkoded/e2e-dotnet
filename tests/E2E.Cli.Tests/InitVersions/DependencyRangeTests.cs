// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitVersions;

/// <summary>
/// Upstream's <c>describe('dependencyRange')</c> of <c>tests/unit/init-versions.test.ts</c>. The ranges are NuGet's: a caret
/// is <c>[0.7.0,0.8.0)</c>, an exact pin <c>[0.8.0-preview.1]</c>, and any <c>0.x</c> is <c>0.*</c>. The port has no
/// recorded sibling versions, so "the engine version the build recorded" is the version of this tool.
/// </summary>
public sealed class DependencyRangeTests
{
    [Theory]
    [InlineData("0.7.0", "[0.7.0,0.8.0)")]
    [InlineData("1.2.3", "[1.2.3,2.0.0)")]
    [InlineData("0.0.3", "[0.0.3,0.0.4)")]
    public void Asks_for_the_minor_of_the_engine_version_the_build_recorded(string version, string range)
    {
        Assert.Equal(range, Versions.DependencyRange(version));
    }

    [Fact]
    public void Pins_a_canary_engine_to_the_exact_build_recorded_next_to_the_runner()
    {
        Assert.Equal("[0.8.0-preview.0.12]", Versions.DependencyRange("0.8.0-preview.0.12"));
    }

    [Fact]
    public void Accepts_any_0_x_when_running_from_source_where_nothing_was_recorded()
    {
        Assert.Equal("0.*", Versions.DependencyRange(null));
    }
}
