// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.TargetIdentity;

/// <summary>
/// <c>describe('target identity')</c> of <c>target-identity.test.ts</c>: the identity replay cache and session entries key on, pinned
/// byte for byte. The digests were computed before the app moved from the engine's options to the target's <c>app</c>, so the same app
/// declared the new way must keep every recording it made.
/// </summary>
public sealed class TargetIdentityTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-target-identity");

    /// <summary>An engine standing in for web() or mobile(): only its name, version, and platform reach the identity.</summary>
    private sealed class IdentityEngine(string name, string platform) : IEngine
    {
        public string Name => name;

        public string Platform => platform;

        public string Version => "1.2.3";

        public EngineCapabilities Capabilities => EngineCapabilities.None;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    public static TheoryData<string, string, string, string, string, string> Targets => new()
    {
        {
            "a web app on a free port, keeping the declared :0", "chromium", "web",
            """{ "url": "http://127.0.0.1:0/app/", "command": { "executable": "node", "args": ["server.js", "{port}"] } }""",
            "web", "523f3dfa502c50418a9443ae5ceb42dc8d1f671cd5108fd96d0d75270f8c534b"
        },
        {
            "a staging web app", "staging", "web", """{ "url": "https://staging.shop.dev", "environment": "staging" }""",
            "web", "899e517e7f0e747facda088c5a45964dc5548314f50680d4095e6f74eb299157"
        },
        {
            "a preview with an explicit identity", "preview", "web", """{ "url": "https://pr-1.preview.dev", "identity": "shop" }""",
            "web", "564358d83d8dcd7955d6793d5872f8c2f0830ef5ca03ebf343970456e2d59e54"
        },
        {
            "a device app by bundle id, beside its build", "ios", "ios", """{ "bundleId": "dev.shop.app", "appPath": "./build/Shop.app" }""",
            "mobile", "b5bc6a10395c7172343172f0b5ed1fab7dcb73c839c7a7756d339d02f7dafcb0"
        },
        {
            "a device app by its build alone", "android", "android", """{ "appPath": "./build/shop.apk" }""",
            "mobile", "a7df5f55b4b581f3f8c97ff111f527376a6093f8e24f98badfa8f23faf5d03d8"
        },
        {
            "a device app with an explicit identity and environment", "ios2", "ios",
            """{ "bundleId": "dev.shop.app", "identity": "shop-ios", "environment": "staging" }""",
            "mobile", "3e1a4768138dcfb789a335b5cd12e679c7c0818d71dea9d2a85c996e63b95616"
        },
    };

    [Theory]
    [MemberData(nameof(Targets))]
    public void Keys_a_target_as_it_did_before_the_move(string label, string name, string platform, string app, string engineName, string appIdentity)
    {
        Assert.NotEmpty(label);
        var target = E2EConfig.Parse($$"""{ "targets": [{ "name": "{{name}}", "platform": "{{platform}}", "app": {{app}} }] }""", Root).Target;
        var resolved = TargetApps.ResolveTargetApp(target.Name, target.App, Root, port: 45678);

        var identity = Sessions.TargetIdentity(target.Name, target.Platform, new IdentityEngine(engineName, platform), resolved);

        Assert.Equal(new SessionIdentity(name, engineName, "1.2.3", 1, platform, appIdentity), identity);
    }
}
