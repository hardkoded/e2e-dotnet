// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace E2E.Tests;

/// <summary>Upstream's <c>tests/helpers/git-repo.ts</c>: throwaway git repositories for tests that read what git says about a checkout.</summary>
internal static class GitRepo
{
    /// <summary>Runs git in <paramref name="cwd"/> with the user's config shut out, returning trimmed stdout.</summary>
    public static string RunGit(string cwd, params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-c", "commit.gpgsign=false", "-c", "init.defaultBranch=main" }.Concat(args))
        {
            info.ArgumentList.Add(arg);
        }

        // A hook or shell that points git elsewhere must not move the throwaway repository.
        foreach (var inherited in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE" })
        {
            info.Environment.Remove(inherited);
        }

        info.Environment["GIT_CONFIG_GLOBAL"] = Path.DirectorySeparatorChar == '\\' ? "NUL" : "/dev/null";
        info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_AUTHOR_NAME"] = "e2e";
        info.Environment["GIT_AUTHOR_EMAIL"] = "e2e@example.test";
        info.Environment["GIT_COMMITTER_NAME"] = "e2e";
        info.Environment["GIT_COMMITTER_EMAIL"] = "e2e@example.test";
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("git " + string.Join(' ', args) + " did not finish in 30 s");
        }

        return process.ExitCode == 0 ? stdout.Result.Trim() : throw new InvalidOperationException("git " + string.Join(' ', args) + " failed: " + stderr.Result);
    }

    /// <summary>Initializes <paramref name="dir"/> as a repository on <c>main</c> with one commit of <c>file.txt</c>, and returns that commit.</summary>
    public static string InitRepo(string dir)
    {
        RunGit(dir, "init", "--quiet");
        File.WriteAllText(Path.Combine(dir, "file.txt"), "one\n");
        RunGit(dir, "add", "file.txt");
        RunGit(dir, "commit", "--quiet", "-m", "one");
        return RunGit(dir, "rev-parse", "HEAD");
    }
}
