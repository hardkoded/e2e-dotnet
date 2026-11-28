// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;

namespace E2E.Desktop.Tests.RealPath;

/// <summary>
/// .NET-only: upstream has no test of <c>fs.realpath</c>, which <c>init</c> uses on the bundle it launches. <see cref="LaunchOwnership.RealPath"/>
/// stands in for it, and <c>FileSystemInfo.ResolveLinkTarget</c> resolves a link at the end of a path only.
/// </summary>
public sealed class RealPathTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("e2e-desktop-realpath-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Fails_on_a_link_that_points_at_itself()
    {
        var root = LaunchOwnership.RealPath(_directory);
        var loop = Path.Combine(root, "Loop.app");
        Directory.CreateSymbolicLink(loop, loop);

        Assert.Throws<IOException>(() => LaunchOwnership.RealPath(loop));
    }

    [Fact]
    public void Resolves_a_link_in_the_middle_of_a_path_and_one_that_points_at_another_link()
    {
        var root = LaunchOwnership.RealPath(_directory);
        var real = Path.Combine(root, "real");
        Directory.CreateDirectory(Path.Combine(real, "Built.app"));
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        Directory.CreateSymbolicLink(first, real);
        Directory.CreateSymbolicLink(second, "first");

        Assert.Equal(Path.Combine(real, "Built.app"), LaunchOwnership.RealPath(Path.Combine(second, "Built.app")));
        Assert.Equal(real, LaunchOwnership.RealPath(first));
    }

    [Fact]
    public void Keeps_a_path_that_does_not_exist_as_it_is()
    {
        var missing = Path.Combine(LaunchOwnership.RealPath(_directory), "Missing.app");
        Assert.Equal(missing, LaunchOwnership.RealPath(missing));
    }
}
