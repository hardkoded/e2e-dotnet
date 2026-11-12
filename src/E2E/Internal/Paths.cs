// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Internal;

/// <summary>Upstream's <c>internal/paths.ts</c>: the part the test filters read.</summary>
internal static class Paths
{
    private const int MaxLinkHops = 40;

    /// <summary>
    /// <paramref name="target"/> as the filesystem spells it, the way git and the current directory report a directory (upstream's
    /// <c>onDiskPath</c>): symlinks resolved and, on a case-insensitive filesystem, each name in its on-disk case, with a short
    /// Windows name read as the long one. A part that does not exist is kept as typed.
    /// </summary>
    public static string OnDiskPath(string target) => OnDiskPath(target, 0);

    /// <summary>
    /// <paramref name="target"/> relative to the project root once both are resolved through the filesystem, or null when it is not strictly
    /// below the root (upstream's <c>relativeToProjectRoot</c>): an in-project symlink pointing outside is refused, a symlinked project root
    /// still counts as the root, and a name that merely starts with <c>..</c> is an ordinary entry.
    /// </summary>
    public static string? RelativeToProjectRoot(string projectRoot, string target)
    {
        var relative = Path.GetRelativePath(OnDiskPath(projectRoot), OnDiskPath(target));
        var inside = relative.Length > 0 && relative != "." && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
        return inside ? relative : null;
    }

    /// <summary>
    /// <paramref name="target"/> with the symlinks of its deepest existing ancestor resolved and the missing tail appended as written
    /// (upstream's <c>realpathOfExisting</c>). A symlink whose target does not exist yet is followed too, since a write through it lands
    /// where it points.
    /// </summary>
    public static string RealPathOfExisting(string target) => RealPathOfExisting(target, 0);

    /// <summary><paramref name="target"/> as a message shows it, with <c>/</c> separators: relative to the project root when strictly below it (see <see cref="RelativeToProjectRoot"/>), else absolute (upstream's <c>displayPath</c>).</summary>
    public static string DisplayPath(string projectRoot, string target) =>
        (RelativeToProjectRoot(projectRoot, target) ?? target).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>Whether <paramref name="target"/> is a path strictly below the project root; see <see cref="RelativeToProjectRoot"/>.</summary>
    public static bool InsideProjectRoot(string projectRoot, string target) => RelativeToProjectRoot(projectRoot, target) is not null;

    private static string RealPathOfExisting(string target, int hops)
    {
        var full = Path.GetFullPath(target);
        var root = Path.GetPathRoot(full) ?? "";
        var parts = full[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        for (var index = 0; index < parts.Length; index++)
        {
            var next = Path.Combine(current, parts[index]);
            FileSystemInfo entry = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
            string? link;
            try
            {
                link = entry.LinkTarget;
            }
            catch (IOException)
            {
                link = null;
            }

            if (link is not null && hops < MaxLinkHops)
            {
                return RealPathOfExisting(Path.Combine([Path.GetFullPath(link, current), .. parts[(index + 1)..]]), hops + 1);
            }

            if (!entry.Exists && link is null)
            {
                return Path.Combine([next, .. parts[(index + 1)..]]);
            }

            current = next;
        }

        return current;
    }

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
