// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.TestMu.Mobile;

/// <summary>
/// One Appium session on TestMu's hub, and the capabilities that ask for it. TestMu documents one flat
/// <c>LT:Options</c> object that holds everything, the credentials included, so that is the only form sent.
/// </summary>
internal sealed record TestMuSessionRequest
{
    public required MobilePlatform Platform { get; init; }

    /// <summary>False asks for a real device (<c>isRealMobile: true</c>), true for an emulator or simulator.</summary>
    public bool Virtual { get; init; }

    public string? DeviceName { get; init; }

    public string? PlatformVersion { get; init; }

    /// <summary>The uploaded build: <c>lt://APP...</c>.</summary>
    public string? App { get; init; }

    public string? Build { get; init; }

    public string? Name { get; init; }

    public bool? Video { get; init; }

    public bool? Network { get; init; }

    public bool? DeviceLog { get; init; }

    public bool? Console { get; init; }

    /// <summary>How long TestMu keeps an idle session, in seconds. Its default is 120.</summary>
    public int? IdleTimeoutSeconds { get; init; }

    /// <summary>How long TestMu queues the request for a free device, in seconds (300 to 900; its default is 600).</summary>
    public int? QueueTimeoutSeconds { get; init; }

    public string? Region { get; init; }

    /// <summary>Further <c>LT:Options</c> keys, merged last: they win over every key above.</summary>
    public IReadOnlyDictionary<string, JsonNode?>? Capabilities { get; init; }

    /// <summary>The <c>capabilities</c> object a session create sends. It holds the access key: never log it.</summary>
    public JsonObject ToCapabilities(HubCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var options = new JsonObject
        {
            ["w3c"] = true,
            ["platformName"] = Platform.WireName(),
            ["isRealMobile"] = !Virtual,
            ["user"] = credentials.User,
            ["accessKey"] = credentials.AccessKey,
        };
        Put(options, "deviceName", DeviceName);
        Put(options, "platformVersion", PlatformVersion);
        Put(options, "app", App);
        Put(options, "build", Build);
        Put(options, "name", Name);
        Put(options, "video", Video);
        Put(options, "network", Network);
        Put(options, "devicelog", DeviceLog);
        Put(options, "console", Console);
        Put(options, "idleTimeout", IdleTimeoutSeconds);
        Put(options, "queueTimeout", QueueTimeoutSeconds);
        Put(options, "region", Region);
        foreach (var (key, value) in Capabilities ?? new Dictionary<string, JsonNode?>())
        {
            options[key] = value?.DeepClone();
        }

        return new JsonObject { ["alwaysMatch"] = new JsonObject { ["LT:Options"] = options } };
    }

    private static void Put<T>(JsonObject target, string key, T? value)
    {
        if (value is not null)
        {
            target[key] = JsonValue.Create(value);
        }
    }
}
