// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Internal;

/// <summary>Upstream's <c>internal/paths.ts</c>: the part the test filters read.</summary>
internal static class Paths
{
    /// <summary>
    /// <paramref name="target"/> as the filesystem spells it, the way git and the current directory report a directory (upstream's
    /// <c>onDiskPath</c>): symlinks resolved and, on a case-insensitive filesystem, each name in its on-disk case, with a short
    /// Windows name read as the long one. A part that does not exist is kept as typed.
    /// </summary>
    public static string OnDiskPath(string target) => OnDiskPath(target, 0);

    private static string OnDiskPath(string target, int hops)
    {
        var full = Path.GetFullPath(target);
        var root = Path.GetPathRoot(full) ?? "";
        var current = root;
        var parts = full[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            string candidate;
            try
            {
                var name = Directory.Exists(current) && parts[index].IndexOfAny(['*', '?']) < 0
                    ? Directory.EnumerateFileSystemEntries(current, parts[index]).Select(Path.GetFileName).FirstOrDefault()
                    : null;
                if (name is null)
                {
                    return Path.Combine([current, .. parts[index..]]);
                }

                candidate = Path.Combine(current, name);
                if (hops < 40 && new FileInfo(candidate).ResolveLinkTarget(returnFinalTarget: true) is { } link)
                {
                    candidate = OnDiskPath(link.FullName, hops + 1);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return Path.Combine([current, .. parts[index..]]);
            }

            current = candidate;
        }

        return current;
    }
}
