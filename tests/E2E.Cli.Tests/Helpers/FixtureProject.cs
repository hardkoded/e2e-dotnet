// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.Helpers;

/// <summary>A project directory for one server: an <c>e2e.config.json</c> and the files beside it, removed on dispose.</summary>
internal sealed class FixtureProject : IDisposable
{
    public FixtureProject(IReadOnlyDictionary<string, string> files)
    {
        Directory = System.IO.Directory.CreateTempSubdirectory("e2e-mcp-").FullName;
        foreach (var (name, content) in files)
        {
            File.WriteAllText(Path.Combine(Directory, name), content);
        }
    }

    public string Directory { get; }

    /// <summary>A config with one web target on <paramref name="appUrl"/> and the given secrets (JSON members).</summary>
    public static string Config(string appUrl, string secrets = "")
    {
        return "{ \"targets\": [{ \"name\": \"web\", \"platform\": \"web\", \"app\": { \"url\": \"" + appUrl + "\" } }]"
            + (secrets.Length == 0 ? "" : ", \"secrets\": { " + secrets + " }") + " }";
    }

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // A browser that is still closing may hold a file; a temp directory is not worth a failed test.
        }
    }
}
