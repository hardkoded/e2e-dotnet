// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Cli.Mcp;
using E2E.Cli.Tests.Helpers;

namespace E2E.Cli.Tests.McpSessionHost;

/// <summary>
/// .NET-only: upstream has no <c>it</c> that reads <c>project.configDigest</c> from an MCP session's report. Its session passes the resolved
/// <c>config</c> to <c>openStandaloneAttempt</c>; the port passes it in <c>E2ESessionOptions.Config</c>.
/// </summary>
public sealed class SessionReportConfigDigestTests
{
    [Fact]
    public async Task A_sessions_report_carries_the_digest_of_the_config_it_opened_on()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-mcp-digest-").FullName;
        try
        {
            var config = E2EConfig.Parse(FixtureProject.Config("http://127.0.0.1:1", ""), dir);
            using (var host = new SessionHost(new SessionHostOptions
            {
                LocateConfig = _ => Path.Combine(dir, "e2e.config.json"),
                LoadConfig = _ => Task.FromResult(config),
                CreateEngine = (_, _) => new FakeEngine(),
            }))
            {
                await host.OpenAsync(new Mcp.OpenSessionOptions());
                await host.CloseAsync("done");
            }

            var path = Assert.Single(Directory.EnumerateFiles(dir, "report.json", SearchOption.AllDirectories));
            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var digest = report.RootElement.GetProperty("run").GetProperty("project").GetProperty("configDigest").GetString();
            Assert.Equal(HostTestFilters.Resolve(config, []).ConfigDigest, digest);
            Assert.NotEqual(new string('0', 64), digest);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
