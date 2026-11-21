// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.OpenSession;

namespace E2E.Tests.OpenHostDeadline;

/// <summary>
/// Upstream's <c>describe('openHost: deadline')</c> (<c>tests/integration/open-host-deadline.test.ts</c>). Only the <c>it</c> tester-army/e2e#1921 adds is
/// ported here; the others (hardkoded/e2e-dotnet#2002) are not yet. The <c>it</c> is <c>Partial_</c>: upstream calls the global <c>test.setTimeout</c>, which
/// finds the session through <c>globalThis</c>; the port's <see cref="E2ESession.SetTimeout"/> is the session's own, which the fixtures call.
/// </summary>
public sealed class OpenHostDeadlineTests : IDisposable
{
    private const string Config =
        """{ "trace": "on", "targets": [{ "name": "kiosk", "platform": "web", "app": { "url": "http://127.0.0.1:4173/" } }] }""";

    private static readonly IReadOnlyDictionary<string, string?> NoEnv = new Dictionary<string, string?>();

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-open-host-deadline-").FullName;
    private readonly List<E2EHost> _hosts = [];

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.CloseAsync().GetAwaiter().GetResult();
        }

        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Partial_Refuses_test_setTimeout_in_a_hosted_session_which_runs_no_e2e_test()
    {
        File.WriteAllText(Path.Combine(_dir, E2EConfig.FileName), Config);
        var host = await E2EHost.OpenAsync(
            new OpenHostOptions
            {
                Cwd = _dir,
                Env = NoEnv,
                EngineFactory = _ => new HostFakeEngine(),
            }
        );
        _hosts.Add(host);
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "hosted setTimeout", Timeout = TimeSpan.FromSeconds(5) }
        );

        var refused = Assert.Throws<TestException>(() => session.SetTimeout(TimeSpan.FromSeconds(60)));
        Assert.Equal("INVALID_ARGUMENT", refused.Code);
        Assert.Contains("a hosted session runs none", refused.Message);
        Assert.Empty(await session.CloseAsync());
    }
}
