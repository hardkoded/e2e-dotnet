// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using E2E.Cli.Report;

namespace E2E.Cli;

/// <summary>
/// Upstream's <c>cli/cache.ts</c>, <c>e2e cache</c>: what is in the replay cache, and how to get rid of it. The store is a directory of
/// digest-named files, so a committed cache is unreadable without a reader: <c>ls</c> prints each entry's provenance (the test, the target,
/// the instruction digest, its age, and how many actions it would replay), <c>stats</c> sizes the store, and <c>clear</c> empties it.
/// Every command works on files only. The port's config file cannot name a <c>cache.store</c>, so upstream's refusal of a custom store has
/// nothing to refuse here.
/// </summary>
internal static partial class CacheCommand
{
    private const string Subcommands = "ls, clear, stats";

    /// <summary>Upstream's <c>CACHE_DIR_DESCRIPTION</c>: the <c>--cache-dir</c> help line. The port has no <c>e2e run</c> to share it with.</summary>
    internal const string CacheDirDescription =
        "replay cache directory, relative to the project root (default: E2E_CACHE_DIR, else cache.dir in the config, else .e2e/cache)";

    private const string Usage = $"""
        Usage: e2e cache <ls|clear|stats> [options]

        Read the replay cache the runs write, under .e2e/cache by default: what a committed cache holds, how much room it takes, and how to empty it. Reads the same config as e2e explore, so --cache-dir, E2E_CACHE_DIR, cache.dir, and --config decide which store is meant.

        Commands:
          ls      list the cached traces: one row per entry with the test and target it was recorded for, the digest of its instruction, its age, and how many actions a replay would run
          clear   delete every cached trace: the cache files and the directory itself; files the runner never wrote are left alone
          stats   print the entry count and size: the store directory, how many readable entries it holds, and how many bytes they take

        Options:
          --config <path>      config file (default: the nearest e2e.config.json)
          --cache-dir <dir>    {CacheDirDescription}

        Examples:
          e2e cache ls
          e2e cache stats
          e2e cache clear
        """;

    /// <summary>Digest prefix printed instead of the full 64 hex characters.</summary>
    private const int DigestPrefixChars = 12;

    /// <summary>Store file names: the key digest, as <see cref="FileStepCache"/> writes them (upstream's <c>ENTRY_FILE</c>).</summary>
    [GeneratedRegex(@"^([a-f0-9]{64})\.json\z", RegexOptions.CultureInvariant)]
    private static partial Regex EntryFile();

    /// <summary>A crashed writer's leftover; a store file for cleanup purposes (upstream's <c>TEMPORARY_FILE</c>).</summary>
    [GeneratedRegex(@"^[a-f0-9]{64}\.json\.[a-f0-9]{16}\.tmp\z", RegexOptions.CultureInvariant)]
    private static partial Regex TemporaryFile();

    /// <summary>
    /// Runs one <c>e2e cache</c> command against the configured store directory (upstream's <c>cache</c>). Returns the process exit code: 2 for a
    /// config or filesystem error, 0 otherwise, since an empty or absent cache is a fact, not a failure. <paramref name="cwd"/> and
    /// <paramref name="env"/> are the process's by default.
    /// </summary>
    public static int Run(
        IEnumerable<string> args,
        TextWriter stdout,
        TextWriter stderr,
        bool interactive,
        string? cwd = null,
        IReadOnlyDictionary<string, string?>? env = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        var parsed = Parse(args);
        if (parsed.Help)
        {
            stdout.WriteLine(Usage);
            return 0;
        }

        if (parsed.Error is not null)
        {
            stderr.WriteLine("error: " + parsed.Error);
            return 2;
        }

        string directory;
        try
        {
            directory = ResolveCacheDirectory(parsed, Path.GetFullPath(cwd ?? Directory.GetCurrentDirectory()), env);
        }
        catch (ConfigurationException ex)
        {
            stderr.WriteLine(ex.Message);
            return 2;
        }

        try
        {
            var contents = ReadContents(directory);
            return parsed.Command switch
            {
                "ls" => List(contents, stdout, stderr, new Colors(interactive && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))),
                "stats" => Stats(contents, stdout, stderr),
                _ => Clear(contents, stdout, stderr),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine("replay cache at " + directory + " could not be read: " + ex.Message);
            return 2;
        }
    }

    /// <summary>
    /// The store directory from the project's resolved config, so <c>--cache-dir</c>, <c>E2E_CACHE_DIR</c>, <c>cache.dir</c>, and <c>--config</c>
    /// mean here what they mean to a run (upstream's <c>resolveCacheDirectory</c>).
    /// </summary>
    private static string ResolveCacheDirectory(CacheArgs args, string cwd, IReadOnlyDictionary<string, string?>? env)
    {
        string path;
        if (args.Config is not null)
        {
            var absolute = Path.GetFullPath(args.Config, cwd);
            path = File.Exists(absolute) ? absolute : throw E2EConfig.ExplicitConfigNotFound(cwd, args.Config, absolute);
        }
        else
        {
            path = E2EConfig.Find(cwd) ?? throw new ConfigurationException("CONFIG_NOT_FOUND", "no " + E2EConfig.FileName + " found from " + cwd + " upward; create one");
        }

        Func<string, string?> environment = env is null ? Environment.GetEnvironmentVariable : name => env.GetValueOrDefault(name);
        var config = E2EConfig.Load(path, environment);
        return args.CacheDir is { } flag ? Path.GetFullPath(flag, config.ProjectRoot) : Path.GetFullPath(config.Cache.Directory);
    }

    /// <summary>
    /// Every file in the store directory, classified (upstream's <c>readContents</c>). Reads go through the store itself, so a file this CLI calls an
    /// entry is exactly one a run would replay: an oversized or unparseable file is unreadable here and a miss there. A directory that does not
    /// exist is an empty store.
    /// </summary>
    private static CacheContents ReadContents(string directory)
    {
        var store = new FileStepCache(directory, FileStepCache.MaxCacheWireBytes, writable: false);
        var entries = new List<CacheEntrySummary>();
        var unreadable = new List<string>();
        var foreign = new List<string>();
        if (!Directory.Exists(directory))
        {
            return new CacheContents(directory, entries, unreadable, foreign);
        }

        foreach (var name in Directory.EnumerateFiles(directory).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal))
        {
            if (EntryFile().Match(name) is not { Success: true } match)
            {
                if (!TemporaryFile().IsMatch(name))
                {
                    foreign.Add(name);
                }

                continue;
            }

            var keyHash = match.Groups[1].Value;
            if (store.Read(keyHash).Entry is not { } entry)
            {
                unreadable.Add(name);
                continue;
            }

            var file = new FileInfo(Path.Combine(directory, name));
            entries.Add(new CacheEntrySummary(
                keyHash,
                file.Length,
                file.LastWriteTimeUtc,
                entry.Actions.Count,
                entry.Truncated == true,
                entry.Test,
                entry.TargetId,
                entry.Instruction is null ? null : Identity.InstructionDigest(entry.Instruction)));
        }

        return new CacheContents(directory, entries, unreadable, foreign);
    }

    /// <summary>
    /// Prints one row per entry, sorted by test, so a reviewer reads a committed cache test by test. Provenance an entry does not carry, one
    /// recorded before entries described themselves, prints as <c>-</c>.
    /// </summary>
    private static int List(CacheContents contents, TextWriter stdout, TextWriter stderr, Colors colors)
    {
        if (contents.Entries.Count == 0)
        {
            stdout.WriteLine("no replay cache entries in " + contents.Directory);
            WarnAboutSkipped(contents, stderr);
            return 0;
        }

        var now = DateTime.UtcNow;
        var rows = contents.Entries
            .OrderBy(entry => entry.TestId ?? "", StringComparer.InvariantCulture)
            .ThenBy(entry => entry.TargetId ?? "", StringComparer.InvariantCulture)
            .ThenBy(entry => entry.CreatedAt)
            .Select(entry => new[]
            {
                entry.TestId ?? "-",
                entry.TargetId ?? "-",
                entry.InstructionDigest is { } digest ? digest[..Math.Min(DigestPrefixChars, digest.Length)] : "-",
                FormatAge(entry.CreatedAt, now),
                entry.Actions.ToString(CultureInfo.InvariantCulture) + (entry.Truncated ? " (truncated)" : ""),
            })
            .ToList();
        string[] header = ["TEST", "TARGET", "INSTRUCTION", "AGE", "ACTIONS"];
        var widths = header.Select((title, column) => Math.Max(Format.VisibleWidth(title), rows.Max(row => Format.VisibleWidth(row[column])))).ToArray();
        string Line(string[] cells) =>
            string.Join("  ", cells.Select((cell, column) => column == cells.Length - 1 ? cell : cell + new string(' ', widths[column] - Format.VisibleWidth(cell)))).TrimEnd();
        stdout.WriteLine(colors.Bold(Line(header)));
        foreach (var row in rows)
        {
            stdout.WriteLine(Line(row));
        }

        WarnAboutSkipped(contents, stderr);
        return 0;
    }

    /// <summary>Prints the store's size: where it is, how many entries, how many bytes.</summary>
    private static int Stats(CacheContents contents, TextWriter stdout, TextWriter stderr)
    {
        var bytes = contents.Entries.Sum(entry => entry.Bytes);
        var rows = new List<(string Label, string Value)>
        {
            ("directory", contents.Directory),
            ("entries", contents.Entries.Count.ToString(CultureInfo.InvariantCulture)),
            ("size", FormatBytes(bytes)),
        };
        if (contents.Unreadable.Count > 0)
        {
            rows.Add(("unreadable", contents.Unreadable.Count.ToString(CultureInfo.InvariantCulture)));
        }

        var width = rows.Max(row => row.Label.Length);
        foreach (var (label, value) in rows)
        {
            stdout.WriteLine(label.PadRight(width) + "  " + value);
        }

        WarnAboutSkipped(contents, stderr);
        return 0;
    }

    /// <summary>
    /// Deletes the store's own files and the directory when nothing else is left in it. Files the store never wrote stay: <c>cache.dir</c> can
    /// point anywhere, and a mistyped one must not take a project's files with it.
    /// </summary>
    private static int Clear(CacheContents contents, TextWriter stdout, TextWriter stderr)
    {
        var removable = contents.Entries.Select(entry => entry.KeyHash + ".json").Concat(contents.Unreadable).ToList();
        foreach (var name in removable)
        {
            File.Delete(Path.Combine(contents.Directory, name));
        }

        // Leftover temporaries are neither entries nor foreign files; they go too.
        var removed = removable.Count + RemoveTemporaries(contents.Directory);
        if (contents.Foreign.Count == 0)
        {
            try
            {
                Directory.Delete(contents.Directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not empty, or already gone.
            }
        }

        stdout.WriteLine(removed == 0
            ? "no replay cache entries to clear in " + contents.Directory
            : "cleared " + removed.ToString(CultureInfo.InvariantCulture) + " file(s) from " + contents.Directory);
        WarnAboutSkipped(contents, stderr);
        return 0;
    }

    private static int RemoveTemporaries(string directory)
    {
        string[] names;
        try
        {
            names = [.. Directory.EnumerateFiles(directory).Select(path => Path.GetFileName(path))];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }

        var removed = 0;
        foreach (var name in names.Where(name => TemporaryFile().IsMatch(name)))
        {
            File.Delete(Path.Combine(directory, name));
            removed++;
        }

        return removed;
    }

    /// <summary>Names what was left alone, so a surprising count has an explanation.</summary>
    private static void WarnAboutSkipped(CacheContents contents, TextWriter stderr)
    {
        if (contents.Unreadable.Count > 0)
        {
            stderr.WriteLine($"{contents.Unreadable.Count} file(s) in {contents.Directory} are not readable trace-1 entries; a run treats each as a miss");
        }

        if (contents.Foreign.Count > 0)
        {
            stderr.WriteLine($"{contents.Foreign.Count} file(s) in {contents.Directory} are not cache files and were left alone: {string.Join(", ", contents.Foreign)}");
        }
    }

    /// <summary>Coarse age of one entry: the unit a reviewer cares about, nothing finer.</summary>
    private static string FormatAge(DateTime createdAt, DateTime now)
    {
        var elapsed = now - createdAt;
        // A future timestamp is a clock difference between two machines, not an age.
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return elapsed < TimeSpan.Zero ? "0m" : "<1m";
        }

        var minutes = (long)elapsed.TotalMinutes;
        if (minutes < 60)
        {
            return minutes.ToString(CultureInfo.InvariantCulture) + "m";
        }

        var hours = minutes / 60;
        return hours < 24 ? hours.ToString(CultureInfo.InvariantCulture) + "h" : (hours / 24).ToString(CultureInfo.InvariantCulture) + "d";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        }

        var kib = bytes / 1024.0;
        return kib < 1024
            ? kib.ToString("F1", CultureInfo.InvariantCulture) + " KiB"
            : (kib / 1024).ToString("F1", CultureInfo.InvariantCulture) + " MiB";
    }

    private static CacheArgs Parse(IEnumerable<string> args)
    {
        string? command = null;
        string? config = null;
        string? cacheDir = null;
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var arg = list[i];
            var eq = arg.StartsWith("--", StringComparison.Ordinal) ? arg.IndexOf('=', StringComparison.Ordinal) : -1;
            var name = eq >= 0 ? arg[..eq] : arg;
            switch (name)
            {
                case "-h" or "--help":
                    return new CacheArgs { Help = true };
                case "--config" or "--cache-dir":
                    string value;
                    if (eq >= 0)
                    {
                        value = arg[(eq + 1)..];
                    }
                    else if (i + 1 < list.Count)
                    {
                        value = list[++i];
                    }
                    else
                    {
                        return new CacheArgs { Error = "option '" + name + (name == "--config" ? " <path>" : " <dir>") + "' argument missing" };
                    }

                    if (name == "--config")
                    {
                        config = value;
                    }
                    else
                    {
                        cacheDir = value;
                    }

                    break;
                default:
                    if (arg.StartsWith('-') && arg.Length > 1)
                    {
                        return new CacheArgs { Error = "unknown option '" + arg + "'" };
                    }

                    if (command is not null)
                    {
                        return new CacheArgs { Error = "too many arguments for '" + command + "'. Expected 0 arguments but got 1." };
                    }

                    if (arg is not ("ls" or "clear" or "stats"))
                    {
                        return new CacheArgs { Error = "unknown command '" + arg + "' (expected one of " + Subcommands + ")" };
                    }

                    command = arg;
                    break;
            }
        }

        return command is null ? new CacheArgs { Help = true } : new CacheArgs { Command = command, Config = config, CacheDir = cacheDir };
    }

    private sealed class CacheArgs
    {
        public bool Help { get; init; }

        public string? Error { get; init; }

        public string Command { get; init; } = "";

        public string? Config { get; init; }

        public string? CacheDir { get; init; }
    }

    /// <summary>One entry as <c>ls</c> prints it (upstream's <c>CacheEntrySummary</c>). <c>CreatedAt</c> is the file's last write time: the port's entries carry no <c>createdAt</c>.</summary>
    private sealed record CacheEntrySummary(
        string KeyHash,
        long Bytes,
        DateTime CreatedAt,
        int Actions,
        bool Truncated,
        string? TestId,
        string? TargetId,
        string? InstructionDigest);

    private sealed record CacheContents(string Directory, IReadOnlyList<CacheEntrySummary> Entries, IReadOnlyList<string> Unreadable, IReadOnlyList<string> Foreign);
}
