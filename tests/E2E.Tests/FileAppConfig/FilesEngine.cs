// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.FileAppConfig;

/// <summary>Upstream's <c>defineEngine({ name, version, spiVersion, fileApps, observe, validateApp })</c> of file-app-config.test.ts: an engine that may declare it opens a file app.</summary>
internal sealed class FilesEngine(
    string name,
    bool fileApps,
    Action<EngineAppDeclaration, EngineAppCheckInfo>? validateApp = null
) : IEngine
{
    public string Name => name;

    public string Platform => "web";

    public string Version => "1.0.0";

    public EngineCapabilities Capabilities => EngineCapabilities.Observation;

    public bool FileApps => fileApps;

    public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    void IEngine.ValidateApp(EngineAppDeclaration app, EngineAppCheckInfo info) => validateApp?.Invoke(app, info);
}
