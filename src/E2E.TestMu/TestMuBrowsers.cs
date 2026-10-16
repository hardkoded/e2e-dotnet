// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.TestMu;

/// <summary>
/// TestMu AI (formerly LambdaTest) web grid for <see cref="WebEngineOptions.Browser"/>: one hosted Chromium session per
/// lease, reached at <c>wss://cdp.lambdatest.com/playwright</c>. The grid starts the session when the engine connects,
/// so acquire only builds the endpoint, and it reads <c>LT_USERNAME</c> and <c>LT_ACCESS_KEY</c> (or
/// <c>TESTMU_USERNAME</c> and <c>TESTMU_ACCESS_KEY</c>) from the run's environment. The grid ends the session when the
/// connection closes, so release calls nothing. The dashboard shows the sessions without a Passed or Failed mark,
/// because the engine passes a provider no test result.
/// </summary>
public sealed class TestMuBrowsers(TestMuBrowsersOptions? options = null) : IBrowserProvider
{
    private const string Endpoint = "wss://cdp.lambdatest.com/playwright";

    private readonly TestMuBrowsersOptions _options = options ?? new TestMuBrowsersOptions();

    public string Name => "testmu";

    public Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = Credential(request.Env, "LT_USERNAME", "TESTMU_USERNAME");
        var accessKey = Credential(request.Env, "LT_ACCESS_KEY", "TESTMU_ACCESS_KEY");
        var name = $"{request.TargetName} {request.AttemptId ?? $"slot {request.Slot + 1} of {request.Slots}"}";
        // The id is the session's name: the access key is in the endpoint, and the id is printed in progress lines.
        var capabilities = Capabilities(request, name, user, accessKey);
        return Task.FromResult(new BrowserLease
        {
            Id = name,
            CdpEndpoint = $"{Endpoint}?capabilities={Uri.EscapeDataString(capabilities.ToJsonString())}",
        });
    }

    /// <summary>Nothing to call: the grid ends a session when the engine closes its connection.</summary>
    public Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    private JsonObject Capabilities(BrowserRequest request, string name, string user, string accessKey)
    {
        var lt = new JsonObject
        {
            ["platform"] = _options.Platform ?? "Windows 10",
            ["build"] = _options.Build ?? $"e2e-{request.RunId}",
            ["name"] = name,
            ["user"] = user,
            ["accessKey"] = accessKey,
        };
        Set(lt, "video", _options.Video);
        Set(lt, "network", _options.Network);
        Set(lt, "console", _options.Console);
        Set(lt, "tunnel", _options.TunnelName is null ? null : true);
        Set(lt, "tunnelName", _options.TunnelName);
        Set(lt, "geoLocation", _options.GeoLocation);
        foreach (var (key, value) in _options.Capabilities ?? new Dictionary<string, JsonNode?>())
        {
            lt[key] = value?.DeepClone();
        }

        return new JsonObject
        {
            ["browserName"] = _options.BrowserName ?? "Chrome",
            ["browserVersion"] = _options.BrowserVersion ?? "latest",
            ["LT:Options"] = lt,
        };
    }

    private static void Set(JsonObject target, string key, object? value)
    {
        if (value is not null)
        {
            target[key] = value switch { bool flag => flag, string text => text, _ => throw new ArgumentException("Not a capability value.", nameof(value)) };
        }
    }

    private static string Credential(IReadOnlyDictionary<string, string?> env, string name, string alias)
    {
        foreach (var variable in new[] { name, alias })
        {
            if (env.GetValueOrDefault(variable) is { Length: > 0 } value)
            {
                return value;
            }
        }

        throw new InvalidOperationException($"TestMuBrowsers needs {name} (or {alias}) in the run environment. Both are in the TestMu automation dashboard, under Access Key.");
    }
}
