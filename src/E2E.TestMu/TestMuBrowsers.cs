// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.TestMu;

/// <summary>
/// TestMu AI (formerly LambdaTest) web grid for <see cref="WebEngineOptions.Browser"/>: one hosted Chromium session per
/// lease, reached at <c>wss://cdp.lambdatest.com/playwright</c> with Playwright's own protocol (the URL is named
/// for CDP, but the grid does not answer it). The grid starts the session when the engine connects,
/// so acquire only builds the endpoint, and it reads <c>LT_USERNAME</c> and <c>LT_ACCESS_KEY</c> (or
/// <c>TESTMU_USERNAME</c> and <c>TESTMU_ACCESS_KEY</c>) from the run's environment. The grid ends the session when the
/// connection closes, so release calls nothing. The dashboard shows the sessions without a Passed or Failed mark,
/// because the engine passes a provider no test result.
/// </summary>
public sealed class TestMuBrowsers(TestMuBrowsersOptions? options = null) : IBrowserProvider
{
    private const string Endpoint = "wss://cdp.lambdatest.com/playwright";

    // The hub refuses a session without a platform.
    private const string DefaultPlatform = "Windows 11";

    // The hub's Edge connect never completes without a user data directory.
    private const string EdgeUserDataDir = "--user-data-dir=remote-debug-profile-edge";

    // The hub ends a session after 300 s without client traffic, shorter than a slow test's gaps.
    private const int DefaultIdleTimeoutSeconds = 600;

    private static readonly string[] ReservedCapabilities = ["user", "accessKey", "name"];

    private readonly TestMuBrowsersOptions _options = Validated(options ?? new TestMuBrowsersOptions());

    public string Name => "testmu";

    public Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (user, accessKey) = TestMuCredentials.Get(request.Env);
        var name = Dashboard.DefaultSessionName(request);
        // The id is the session's name: the access key is in the endpoint, and the id is printed in progress lines.
        var capabilities = Capabilities(request, name, user, accessKey);
        return Task.FromResult(new BrowserLease
        {
            Id = name,
            Protocol = BrowserProtocol.Playwright,
            CdpEndpoint = $"{Endpoint}?capabilities={Uri.EscapeDataString(capabilities.ToJsonString())}",
        });
    }

    /// <summary>Nothing to call: the grid ends a session when the engine closes its connection.</summary>
    public Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    private JsonObject Capabilities(BrowserRequest request, string name, string user, string accessKey)
    {
        var lt = new JsonObject
        {
            ["platform"] = _options.Platform ?? DefaultPlatform,
            ["project"] = Dashboard.DefaultProject,
            ["build"] = _options.Build ?? $"e2e-{request.RunId}",
            ["idleTimeout"] = DefaultIdleTimeoutSeconds,
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

        if (_options.BrowserName == "MicrosoftEdge")
        {
            var flags = lt["browserOptions"] as JsonArray ?? [];
            if (!flags.Any(flag => ((string?)flag)!.StartsWith("--user-data-dir=", StringComparison.Ordinal)))
            {
                flags.Add(EdgeUserDataDir);
            }

            lt["browserOptions"] = flags;
        }

        return new JsonObject
        {
            ["browserName"] = _options.BrowserName ?? "Chrome",
            ["browserVersion"] = _options.BrowserVersion ?? "latest",
            ["LT:Options"] = lt,
        };
    }

    private static TestMuBrowsersOptions Validated(TestMuBrowsersOptions options)
    {
        foreach (var (key, value) in options.Capabilities ?? new Dictionary<string, JsonNode?>())
        {
            AssertJsonSafe(value, $"Capabilities[\"{key}\"]");
            try
            {
                value?.ToJsonString();
            }
            catch (Exception error) when (error is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
            {
                // A JsonValue can wrap a custom object that JSON cannot encode.
                throw Invalid($"`Capabilities[\"{key}\"]` cannot be encoded as JSON: {error.Message}");
            }
        }

        var capabilities = options.Capabilities;
        if (capabilities?.ContainsKey("LT:Options") == true)
        {
            throw Invalid("pass `LT:Options` fields directly in `Capabilities`, not nested under `LT:Options`");
        }

        if (capabilities?.ContainsKey("browserName") == true)
        {
            throw Invalid("set `browserName` as an option (`BrowserName`), not in `Capabilities`");
        }

        var reserved = ReservedCapabilities.Where(key => capabilities?.ContainsKey(key) == true).ToArray();
        if (reserved.Length > 0)
        {
            throw Invalid($"`Capabilities` cannot set {string.Join(", ", reserved.Select(key => $"`{key}`"))}; the provider names each session, and reads LT_USERNAME and LT_ACCESS_KEY from the environment");
        }

        if (capabilities is not null
            && capabilities.TryGetValue("browserOptions", out var browserOptions)
            && !(browserOptions is JsonArray flags && flags.All(flag => flag is JsonValue value && value.TryGetValue<string>(out _))))
        {
            throw Invalid("`Capabilities[\"browserOptions\"]` must be an array of strings");
        }

        // A copy, so the caller cannot add a reserved entry to its own dictionary after the check.
        return capabilities is null ? options : options with { Capabilities = capabilities.ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone()) };
    }

    /// <summary>A <see cref="JsonNode"/> cannot hold a cycle, a map, or a date. Only a NaN or an infinity has no JSON form.</summary>
    private static void AssertJsonSafe(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonValue value when (value.TryGetValue<double>(out var number) && !double.IsFinite(number))
                || (value.TryGetValue<float>(out var single) && !float.IsFinite(single)):
                throw Invalid($"`{path}` contains a non-finite number");
            case JsonObject obj:
                foreach (var (key, child) in obj)
                {
                    AssertJsonSafe(child, $"{path}.{key}");
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    AssertJsonSafe(array[i], $"{path}[{i}]");
                }

                break;
        }
    }

    private static ConfigurationException Invalid(string message) => new("INVALID_CONFIG", $"TestMuBrowsers: {message}");

    private static void Set(JsonObject target, string key, bool? value)
    {
        if (value is { } flag)
        {
            target[key] = flag;
        }
    }

    private static void Set(JsonObject target, string key, string? value)
    {
        if (value is not null)
        {
            target[key] = value;
        }
    }
}
