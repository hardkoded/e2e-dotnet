// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Cli;

/// <summary>A place project skills live, with the agents that read it.</summary>
/// <param name="Dir">Project-relative skills directory with <c>/</c> separators.</param>
/// <param name="Hint">The agents that read it.</param>
/// <param name="LinksTo">The location whose copy this one links to when both are chosen, so one copy serves every agent: the layout <c>npx skills add</c> produces.</param>
internal sealed record SkillLocation(string Dir, string Hint, string? LinksTo = null);

/// <summary>A symlink on the way to a skill file.</summary>
/// <param name="Relative">Project-relative path of the link with <c>/</c> separators, e.g. <c>.claude/skills/e2e-dotnet</c>.</param>
/// <param name="Target">Absolute path the link leads to; the path as written when it dangles.</param>
internal sealed record SkillLink(string Relative, string Target);

/// <summary>An entry a write cannot pass: a directory where a bundled file goes, or a file where its directory goes.</summary>
/// <param name="Relative">Path relative to the skill directory with <c>/</c> separators, e.g. <c>SKILL.md</c>.</param>
/// <param name="Kind"><c>directory</c> or <c>file</c>.</param>
internal sealed record SkillObstacle(string Relative, string Kind);

/// <summary>One bundled file planned for writing.</summary>
internal sealed record SkillCopyFile(string Absolute, string Content);

/// <summary>A planned skill write.</summary>
/// <param name="Relative">Project-relative skill directory, e.g. <c>.agents/skills/e2e-dotnet</c>.</param>
/// <param name="Existing">True when the directory already holds a copy: a copy updates it, a link replaces it.</param>
/// <param name="Links">
/// The symlinks between the project root and the files, each once, plus any file that resolves outside the project without one.
/// Init never writes through them: a link to a dotfiles repo or a shared skills folder would be overwritten there.
/// </param>
/// <param name="Obstacles">Entries the writes would fail on, each once; removing them is not a repair init makes.</param>
internal abstract record SkillInstall(string Relative, bool Existing, IReadOnlyList<SkillLink> Links, IReadOnlyList<SkillObstacle> Obstacles);

/// <summary>The bundled files, written into the skill directory.</summary>
internal sealed record SkillCopy(string Relative, bool Existing, IReadOnlyList<SkillLink> Links, IReadOnlyList<SkillObstacle> Obstacles, IReadOnlyList<SkillCopyFile> Files)
    : SkillInstall(Relative, Existing, Links, Obstacles);

/// <summary>A symlink standing in for the skill directory, leading to the copy in another location.</summary>
/// <param name="Relative">Project-relative link, with <c>/</c> separators.</param>
/// <param name="Existing">True when a copy stands where the link goes.</param>
/// <param name="Links">Symlinks met on the way.</param>
/// <param name="Obstacles">Entries the write cannot pass.</param>
/// <param name="Target">The link as written, relative to its parent with <c>/</c> separators: <c>../../.agents/skills/e2e-dotnet</c>.</param>
internal sealed record SkillSymlink(string Relative, bool Existing, IReadOnlyList<SkillLink> Links, IReadOnlyList<SkillObstacle> Obstacles, string Target)
    : SkillInstall(Relative, Existing, Links, Obstacles);

/// <summary>Upstream's <c>cli/init/agent-skill.ts</c>: the <c>e2e init</c> step that installs the bundled skill for coding agents.</summary>
internal static class AgentSkill
{
    /// <summary>Where project skills live: most agents read <c>.agents/skills</c>; Claude Code reads <c>.claude/skills</c>.</summary>
    public static readonly IReadOnlyList<SkillLocation> SkillLocations =
    [
        new(".agents/skills", "Codex, Cursor, Copilot, Gemini CLI, OpenCode, Zed, and most other agents"),
        new(".claude/skills", "Claude Code, as a symlink into .agents/skills when both are chosen", ".agents/skills"),
    ];

    private enum BlockerKind
    {
        Link,
        Directory,
        File,
    }

    private sealed record Blocker(BlockerKind Kind, string Absolute);

    private sealed record Entry(bool IsSymbolicLink, bool IsDirectory, bool IsFile);

    /// <summary>
    /// Known locations that hold a copy of the skill. The directory is the marker, not <c>SKILL.md</c>, so a copy that lost
    /// files is repaired rather than ignored.
    /// </summary>
    public static List<string> FindInstalledSkillDirs(string cwd) =>
        [.. SkillLocations.Select(location => location.Dir).Where(dir => Exists(Path.Combine(cwd, dir, Skill.Name)))];

    /// <summary>The chosen directories split into the ones written as a copy and the ones linked to a copy among them.</summary>
    public static (List<string> Copies, List<(string Dir, string LinksTo)> Links) SplitSkillDirs(IReadOnlyList<string> dirs)
    {
        var links = SkillLocations
            .Where(location => location.LinksTo is not null && dirs.Contains(location.Dir) && dirs.Contains(location.LinksTo))
            .Select(location => (location.Dir, location.LinksTo!))
            .ToList();
        return ([.. dirs.Where(dir => !links.Any(link => link.Dir == dir))], links);
    }

    /// <summary>
    /// Plans the writes for the given directories from the bundled files. A directory without the skill is created and one
    /// whose copy differs from the bundle is rewritten; an up-to-date copy needs nothing and is left out. Files the bundle
    /// does not ship are never removed. A location reached through a symlink, or holding an entry a write cannot pass, is
    /// planned with what was met and never read, for the caller to skip. An empty bundle with somewhere to write is a broken
    /// installation, never a set of current copies.
    /// </summary>
    public static List<SkillInstall> PlanSkillInstall(string cwd, IReadOnlyList<string> dirs, IReadOnlyList<SkillFile> bundled)
    {
        if (dirs.Count > 0 && bundled.Count == 0)
        {
            throw new InvalidOperationException(Skill.MissingSkillMessage);
        }

        var installs = new List<SkillInstall>();
        foreach (var dir in dirs)
        {
            var root = Path.GetFullPath(Path.Combine(cwd, dir, Skill.Name));
            var files = bundled.Select(file => new SkillCopyFile(Path.Combine(root, file.Relative), file.Content)).ToList();
            var (links, obstacles) = FindBlockers(cwd, root, files.Select(file => file.Absolute));
            var current = links.Count == 0 && obstacles.Count == 0
                && files.All(file => CurrentContent(file.Absolute) == file.Content);
            if (current)
            {
                continue;
            }

            installs.Add(new SkillCopy(dir + "/" + Skill.Name, Exists(root), links, obstacles, files));
        }

        return installs;
    }

    /// <summary>
    /// Whether a directory holds the copy once the planned writes are done: its plan goes ahead, or nothing was planned
    /// because the copy is current.
    /// </summary>
    public static bool HoldsCopyAfter(string dir, IReadOnlyList<SkillInstall> planned, IReadOnlyList<SkillInstall> going)
    {
        var relative = dir + "/" + Skill.Name;
        return going.Any(install => install.Relative == relative) || !planned.Any(install => install.Relative == relative);
    }

    /// <summary>
    /// Plans <paramref name="dir"/> as a symlink to the copy in <paramref name="canonicalDir"/>, which <paramref name="ready"/>
    /// says this run leaves in place. Nothing is planned for an entry that already leads there, the link itself or a linked
    /// parent. A copy holding only bundled files is replaced by the link, since the copy it leads to has the same content; one
    /// holding anything else, like a location whose copy is not ready, is planned as a copy like any other. A symlink leading
    /// elsewhere, or one on the way, is planned with what was met, for the caller to skip.
    /// </summary>
    public static SkillInstall? PlanSkillLink(string cwd, string dir, string canonicalDir, IReadOnlyList<SkillFile> bundled, bool ready)
    {
        var link = Path.GetFullPath(Path.Combine(cwd, dir, Skill.Name));
        var canonical = Path.GetFullPath(Path.Combine(cwd, canonicalDir, Skill.Name));
        if (ready && LeadsTo(link, canonical))
        {
            return null;
        }

        var entry = LstatOrUndefined(link);
        var linkable = entry is null || entry.IsSymbolicLink || (entry.IsDirectory && HoldsOnlyBundled(link, bundled));
        if (!ready || !linkable)
        {
            return PlanSkillInstall(cwd, [dir], bundled).FirstOrDefault();
        }

        var blocker = FirstBlocker(cwd, Path.GetDirectoryName(link)!, wantsFile: false)
            ?? (entry?.IsSymbolicLink == true ? new Blocker(BlockerKind.Link, link) : null);
        var found = DescribeBlocker(cwd, link, link, blocker);
        return new SkillSymlink(
            dir + "/" + Skill.Name,
            entry?.IsDirectory == true,
            found?.Link is { } foundLink ? [foundLink] : [],
            found?.Obstacle is { } foundObstacle ? [foundObstacle] : [],
            PosixRelative(Path.GetDirectoryName(link)!, canonical));
    }

    /// <summary>
    /// Whether removing the install's links leaves everything but the skill in place: each is the skill directory or inside
    /// it. A linked parent (<c>.claude/skills</c>) holds more than the skill, so a copy cannot stand in for it.
    /// </summary>
    public static bool ReplaceableLinks(SkillInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        return install.Links.All(link => link.Relative.StartsWith(install.Relative, StringComparison.Ordinal));
    }

    /// <summary>
    /// The install's links for a message: a skill directory that is itself the link reads <c>dir -> target</c>; otherwise the
    /// directory, then the links in parentheses.
    /// </summary>
    public static string DescribeLinks(SkillInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        var links = string.Join(", ", install.Links.Select(link => link.Relative + " -> " + link.Target));
        return install.Links.Count == 1 && install.Links[0].Relative == install.Relative ? links : install.Relative + "/ (" + links + ")";
    }

    /// <summary>The install's obstacles for a message: the directory, then each entry and what it is in parentheses.</summary>
    public static string DescribeObstacles(SkillInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        return install.Relative + "/ (" + string.Join(", ", install.Obstacles.Select(obstacle => obstacle.Relative + " is a " + obstacle.Kind)) + ")";
    }

    /// <summary>The content a bundled file currently has, or null when it cannot be read as a file, which differs from any bundle.</summary>
    private static string? CurrentContent(string absolute)
    {
        try
        {
            return File.ReadAllText(absolute);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether <paramref name="link"/> already leads to <paramref name="canonical"/>: it resolves there through the filesystem,
    /// a linked parent included, or it is a symlink written to point there whose target does not exist yet.
    /// </summary>
    private static bool LeadsTo(string link, string canonical) => Paths.OnDiskPath(link) == Paths.OnDiskPath(canonical);

    /// <summary>
    /// Whether the directory holds nothing but regular files the bundle ships, so a link to another copy loses nothing. A
    /// symlink inside is a link to respect under the copy rules, never a file to drop.
    /// </summary>
    private static bool HoldsOnlyBundled(string root, IReadOnlyList<SkillFile> bundled)
    {
        var shipped = bundled.Select(file => file.Relative).ToList();
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = false };
        foreach (var info in new DirectoryInfo(root).EnumerateFileSystemInfos("*", options))
        {
            var relative = PosixRelative(root, info.FullName);
            if (info.LinkTarget is not null)
            {
                return false;
            }

            var good = info is DirectoryInfo
                ? shipped.Any(file => file.StartsWith(relative + "/", StringComparison.Ordinal))
                : shipped.Contains(relative);
            if (!good)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// What stands between the project root and the given files, each once: the first symlink on a path (components past it
    /// live wherever it points and are not inspected), a file that resolves outside the project without one, and the entries a
    /// write cannot pass.
    /// </summary>
    private static (List<SkillLink> Links, List<SkillObstacle> Obstacles) FindBlockers(string cwd, string root, IEnumerable<string> files)
    {
        var links = new Dictionary<string, SkillLink>(StringComparer.Ordinal);
        var obstacles = new Dictionary<string, SkillObstacle>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var found = DescribeBlocker(cwd, root, file, FirstBlocker(cwd, file, wantsFile: true));
            if (found is null)
            {
                continue;
            }

            if (found.Link is { } link)
            {
                links[link.Relative] = link;
            }
            else if (found.Obstacle is { } obstacle)
            {
                obstacles[obstacle.Relative] = obstacle;
            }
        }

        return ([.. links.Values], [.. obstacles.Values]);
    }

    private sealed record Described(SkillLink? Link, SkillObstacle? Obstacle);

    /// <summary>
    /// A blocker as the plan spells it: a symlink with where it leads, the entry a write cannot pass relative to the skill
    /// directory, or, with no blocker on the path, <paramref name="file"/> itself when it resolves outside the project.
    /// </summary>
    private static Described? DescribeBlocker(string cwd, string root, string file, Blocker? blocker)
    {
        if (blocker is null)
        {
            if (Paths.InsideProjectRoot(cwd, file))
            {
                return null;
            }

            return new Described(new SkillLink(PosixRelative(cwd, file), Paths.OnDiskPath(file)), null);
        }

        if (blocker.Kind == BlockerKind.Link)
        {
            return new Described(new SkillLink(PosixRelative(cwd, blocker.Absolute), LinkTarget(blocker.Absolute)), null);
        }

        return new Described(null, new SkillObstacle(PosixRelative(root, blocker.Absolute), blocker.Kind == BlockerKind.Directory ? "directory" : "file"));
    }

    /// <summary>
    /// The first entry from the project root down to <paramref name="target"/>, wanted as a file or as a directory, that a
    /// write cannot go through as planned.
    /// </summary>
    private static Blocker? FirstBlocker(string cwd, string target, bool wantsFile)
    {
        var segments = Path.GetRelativePath(cwd, target).Split(Path.DirectorySeparatorChar);
        var current = cwd;
        for (var index = 0; index < segments.Length; index++)
        {
            current = Path.Combine(current, segments[index]);
            var entry = LstatOrUndefined(current);
            if (entry is null)
            {
                return null;
            }

            if (entry.IsSymbolicLink)
            {
                return new Blocker(BlockerKind.Link, current);
            }

            var wantsThisFile = wantsFile && index == segments.Length - 1;
            if (wantsThisFile && entry.IsDirectory)
            {
                return new Blocker(BlockerKind.Directory, current);
            }

            if (!wantsThisFile && !entry.IsDirectory)
            {
                return new Blocker(BlockerKind.File, current);
            }
        }

        return null;
    }

    /// <summary>The entry at <paramref name="target"/> without following a final symlink; null when missing or unreachable.</summary>
    private static Entry? LstatOrUndefined(string target)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(target) ? new DirectoryInfo(target) : new FileInfo(target);
            if (info.LinkTarget is not null)
            {
                return new Entry(true, false, false);
            }

            return info.Exists ? new Entry(false, info is DirectoryInfo, info is FileInfo) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Whether <paramref name="path"/> exists once a final symlink is followed: a link to nothing is no entry (upstream's <c>existsSync</c>).</summary>
    internal static bool Exists(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (info.LinkTarget is null)
        {
            return info.Exists;
        }

        try
        {
            return info.ResolveLinkTarget(returnFinalTarget: true)?.Exists == true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>Where a symlink leads: its real path, or the path as written when it dangles.</summary>
    private static string LinkTarget(string link)
    {
        if (Exists(link))
        {
            return Paths.OnDiskPath(link);
        }

        FileSystemInfo info = new FileInfo(link);
        return Path.GetFullPath(info.LinkTarget ?? link, Path.GetDirectoryName(link)!);
    }

    /// <summary><paramref name="absolute"/> relative to <paramref name="from"/> with <c>/</c> separators, as the messages and the plan spell paths.</summary>
    private static string PosixRelative(string from, string absolute) => Path.GetRelativePath(from, absolute).Replace('\\', '/');
}
