// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Cli.Tests.CliInit;

/// <summary>
/// .NET-only test helper standing for the top of upstream's <c>cli-init.test.ts</c>: a throwaway project, the captured
/// output (<c>output()</c>), <c>read</c>, <c>writeCopy</c>, and the <c>symlinks</c> probe.
/// </summary>
public abstract class CliInitTestBase : IDisposable
{
    /// <summary>Whether this host lets the tests create symlinks; Windows needs a privilege for them.</summary>
    protected static readonly bool Symlinks = ProbeSymlinks();

    private readonly StringWriter _output = new();

    protected CliInitTestBase()
    {
        Dir = Directory.CreateTempSubdirectory("e2e-init-").FullName;
    }

    /// <summary>The throwaway project.</summary>
    protected string Dir { get; }

    /// <summary>What init printed so far.</summary>
    protected string Output => _output.ToString();

    public void Dispose()
    {
        try
        {
            Directory.Delete(Dir, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A temp directory left behind is not a test failure.
        }

        GC.SuppressFinalize(this);
    }

    private protected Task<InitRunOutcome> RunInitAsync(InitOptions options, string? root = null) => Init.RunAsync(root ?? Dir, options, _output);

    private protected Task<InitRunOutcome> RunInitAsync(bool yes = true) => RunInitAsync(new InitOptions { Yes = yes });

    protected string Read(string file) => File.ReadAllText(Path.Combine(Dir, file));

    protected string Path_(string file) => Path.Combine(Dir, file);

    protected bool Exists(string file) => File.Exists(Path_(file)) || Directory.Exists(Path_(file));

    protected JsonNode Json(string file) => JsonNode.Parse(Read(file))!;

    protected void ClearOutput() => _output.GetStringBuilder().Clear();

    /// <summary>The bundled SKILL.md.</summary>
    protected static string BundledSkillMd() => E2E.Cli.Skill.ReadFiles().First(file => file.Relative == "SKILL.md").Content;

    /// <summary>Writes the bundled skill into <c>&lt;location&gt;/e2e-dotnet</c> as an earlier init left it, every file holding <paramref name="content"/> when given.</summary>
    protected void WriteCopy(string location, string? content = null)
    {
        foreach (var file in E2E.Cli.Skill.ReadFiles())
        {
            var absolute = Path.Combine(Dir, location, E2E.Cli.Skill.Name, file.Relative);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, content ?? file.Content);
        }
    }

    /// <summary>The path as the filesystem spells it with symlinks resolved (<c>realpathSync</c>).</summary>
    protected static string Real(string path) => Paths.OnDiskPath(path);

    protected static void Link(string link, string target, bool directory)
    {
        if (directory)
        {
            Directory.CreateSymbolicLink(link, target);
        }
        else
        {
            File.CreateSymbolicLink(link, target);
        }
    }

    protected static bool IsLink(string path) => new FileInfo(path).LinkTarget is not null;

    private static bool ProbeSymlinks()
    {
        var probe = Directory.CreateTempSubdirectory("e2e-init-symlink-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(probe, "file"), "");
            File.CreateSymbolicLink(Path.Combine(probe, "link"), Path.Combine(probe, "file"));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            Directory.Delete(probe, recursive: true);
        }
    }
}
