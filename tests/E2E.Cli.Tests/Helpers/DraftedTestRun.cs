// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace E2E.Cli.Tests.Helpers;

/// <summary>
/// Upstream's <c>runProject</c> for a pasted draft: a test class built from the lines of <c>test_lines</c> in a project of its own
/// (beside the project's <c>e2e.config.json</c>), run with <c>dotnet test</c>.
/// </summary>
internal static class DraftedTestRun
{
    /// <summary>Builds and runs one NUnit test whose body is <paramref name="body"/>, then <paramref name="check"/>. The result holds the exit code and the output.</summary>
    public static async Task<(int Code, string Output)> RunAsync(string projectDirectory, IEnumerable<string> body, string check)
    {
        var repo = RepoRoot();
        File.WriteAllText(
            Path.Combine(projectDirectory, "Draft.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
                <PackageReference Include="NUnit" Version="5.0.0" />
                <PackageReference Include="NUnit3TestAdapter" Version="6.3.0" />
              </ItemGroup>
              <ItemGroup>
                <Using Include="NUnit.Framework" />
                <ProjectReference Include="{Path.Combine(repo, "src", "E2E", "E2E.csproj")}" />
                <ProjectReference Include="{Path.Combine(repo, "src", "E2E.NUnit", "E2E.NUnit.csproj")}" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(projectDirectory, "DraftTests.cs"),
            $$"""
            using E2E;
            using E2E.NUnit;

            public sealed class DraftTests : E2ETest
            {
                [Test]
                public async Task Replays_the_drafted_lines()
                {
            {{string.Join('\n', body.Append(check).Select(line => "        " + line))}}
                }
            }
            """);

        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = projectDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("test");
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-nodeReuse:false");
        start.Environment["UseSharedCompilation"] = "false";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // A killed build must not leave its servers holding the pipes and the project directory.
            process.Kill(entireProcessTree: true);
            throw;
        }

        return (process.ExitCode, await stdout + await stderr);
    }

    private static string RepoRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "E2E.slnx")))
        {
            root = root.Parent ?? throw new InvalidOperationException("no E2E.slnx above " + AppContext.BaseDirectory);
        }

        return root.FullName;
    }
}
