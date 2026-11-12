// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RerunArtifacts;

/// <summary>
/// Upstream's helpers in <c>rerun-artifacts.test.ts</c> (<c>plant</c> and <c>tree</c>): an artifact tree in a temporary directory, with files named by report path.
/// </summary>
internal sealed class RerunTree : IDisposable
{
    private readonly string _parent = Directory.CreateTempSubdirectory("e2e-rerun-artifacts-").FullName;

    /// <summary>The results directory; it is not there until something is planted.</summary>
    public string Root => Path.Combine(_parent, "results");

    public void Dispose() => Directory.Delete(_parent, recursive: true);

    /// <summary>Writes a file at a report path under the root, with the path as its content.</summary>
    public void Plant(string reportPath)
    {
        var file = Path.Combine(Root, Path.Combine(reportPath.Split('/')));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, reportPath);
    }

    /// <summary>Every file under the root, as report paths, sorted.</summary>
    public string[] Files() =>
        [.. Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(Root, file).Replace(Path.DirectorySeparatorChar, '/')).Order(StringComparer.Ordinal)];
}
