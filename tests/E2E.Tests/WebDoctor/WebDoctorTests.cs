// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.BrowserProvider;

namespace E2E.Tests.WebDoctor;

/// <summary><c>describe('web doctor')</c> of upstream <c>web/tests/unit/doctor.test.ts</c>: the checks of <c>e2e doctor</c> for a web engine.</summary>
public sealed class WebDoctorTests : IDisposable
{
    private static readonly WebConnectOptions Attach = new() { CdpEndpoint = _ => Task.FromResult("http://127.0.0.1:9222") };

    private readonly string _projectRoot = Directory.CreateTempSubdirectory("e2e-web-doctor-").FullName;

    public void Dispose() => Directory.Delete(_projectRoot, recursive: true);

    private EngineDoctorInfo Info() => new()
    {
        TargetName = "web",
        ProjectRoot = _projectRoot,
        App = new EngineAppInfo(),
        Env = new Dictionary<string, string?>(),
        Headed = false,
        Log = _ => { },
    };

    [Fact]
    public async Task Skips_the_browser_check_for_a_CDP_attach_and_a_browser_provider()
    {
        Assert.Equal(
            [new EngineDoctorCheck("browser", EngineDoctorStatus.Skip, "the browser is attached over CDP")],
            await new WebEngine(new WebEngineOptions { Connect = Attach }).DoctorAsync(Info(), CancellationToken.None));
        Assert.Equal(
            [new EngineDoctorCheck("browser", EngineDoctorStatus.Skip, "browsers come from the \"toy-cloud\" provider")],
            await new WebEngine(new WebEngineOptions { Browser = new ToyCloud() }).DoctorAsync(Info(), CancellationToken.None));
    }

    [Fact]
    public async Task Checks_the_browser_of_a_local_launch()
    {
        var checks = await new WebEngine().DoctorAsync(Info(), CancellationToken.None);
        Assert.Equal(["browser"], checks.Select(check => check.Id));
    }

    [Fact]
    public async Task Fails_an_init_script_file_that_does_not_read_as_prepare_would()
    {
        await File.WriteAllTextAsync(Path.Combine(_projectRoot, "present.js"), "window.present = true;");
        var engine = new WebEngine(new WebEngineOptions { InitScripts = [WebInitScript.FromPath("present.js"), WebInitScript.FromPath("missing.js")], Connect = Attach });
        var scripts = (await engine.DoctorAsync(Info(), CancellationToken.None))[0];
        Assert.Equal(("init-scripts", EngineDoctorStatus.Fail), (scripts.Id, scripts.Status));
        Assert.Contains("missing.js", scripts.Message, StringComparison.Ordinal);
        var present = new WebEngine(new WebEngineOptions { InitScripts = [WebInitScript.FromPath("present.js")], Connect = Attach });
        Assert.Equal(new EngineDoctorCheck("init-scripts", EngineDoctorStatus.Pass, "1 script reads"), (await present.DoctorAsync(Info(), CancellationToken.None))[0]);
    }
}
