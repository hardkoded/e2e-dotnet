// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.TestMu.Mobile.Tests.Session;

public sealed class TestMuSessionRequestTests
{
    private static readonly HubCredentials Credentials = new("someone", "key");

    private static JsonObject Options(TestMuSessionRequest request) =>
        (JsonObject)request.ToCapabilities(Credentials)["alwaysMatch"]!["LT:Options"]!;

    [Fact]
    public void Everything_sits_in_one_flat_LT_Options_object_under_alwaysMatch()
    {
        var capabilities = new TestMuSessionRequest { Platform = MobilePlatform.Android }.ToCapabilities(Credentials);

        Assert.Equal(["alwaysMatch"], capabilities.Select(pair => pair.Key));
        Assert.Equal(["LT:Options"], ((JsonObject)capabilities["alwaysMatch"]!).Select(pair => pair.Key));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void A_real_device_is_isRealMobile_true_and_a_virtual_one_false(bool isVirtual, bool isRealMobile)
    {
        var options = Options(new TestMuSessionRequest { Platform = MobilePlatform.Ios, Virtual = isVirtual });

        Assert.Equal(isRealMobile, options["isRealMobile"]!.GetValue<bool>());
        Assert.Equal("ios", options["platformName"]!.GetValue<string>());
        Assert.True(options["w3c"]!.GetValue<bool>());
    }

    [Fact]
    public void Carries_the_credentials_and_every_option_that_is_set()
    {
        var options = Options(new TestMuSessionRequest
        {
            Platform = MobilePlatform.Android,
            DeviceName = "Galaxy S23",
            PlatformVersion = "14",
            App = "lt://APP1",
            Build = "build-1",
            Name = "slot 0",
            Video = true,
            Network = false,
            DeviceLog = true,
            Console = false,
            IdleTimeoutSeconds = 300,
            QueueTimeoutSeconds = 450,
            Region = "EU",
        });

        Json.AssertEqual(
            """
            {"w3c":true,"platformName":"android","isRealMobile":true,"user":"someone","accessKey":"key","deviceName":"Galaxy S23","platformVersion":"14",
             "app":"lt://APP1","build":"build-1","name":"slot 0","video":true,"network":false,"devicelog":true,"console":false,"idleTimeout":300,"queueTimeout":450,"region":"EU"}
            """,
            options);
    }

    [Fact]
    public void Leaves_out_what_is_not_set()
    {
        var options = Options(new TestMuSessionRequest { Platform = MobilePlatform.Android });

        Assert.Equal(["w3c", "platformName", "isRealMobile", "user", "accessKey"], options.Select(pair => pair.Key));
    }

    [Fact]
    public void Further_capabilities_win_over_the_ones_above()
    {
        var options = Options(new TestMuSessionRequest
        {
            Platform = MobilePlatform.Android,
            Build = "mine",
            Capabilities = new Dictionary<string, JsonNode?> { ["build"] = "theirs", ["tunnel"] = true, ["gone"] = null },
        });

        Assert.Equal("theirs", options["build"]!.GetValue<string>());
        Assert.True(options["tunnel"]!.GetValue<bool>());
        Assert.Null(options["gone"]);
    }
}
