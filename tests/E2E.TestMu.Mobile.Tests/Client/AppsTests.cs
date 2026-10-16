// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Mobile;

namespace E2E.TestMu.Mobile.Tests.Client;

public sealed class AppsTests
{
    private static string[] Scripts(FakeAppium hub) => [.. hub.Scripts().Select(call => $"{call.Script} {call.Arguments?.ToJsonString()}".Trim())];

    [Fact]
    public async Task Brings_an_app_to_the_foreground_and_names_it_as_the_engine_did()
    {
        var (client, hub, _) = Appium.Over();

        var result = await client.CallAsync(DeviceMethods.AppsOpen, """{"app":"com.example.app","platform":"ios"}""");

        Assert.Equal(["mobile: activateApp {\"bundleId\":\"com.example.app\"}"], Scripts(hub));
        Json.AssertEqual("""{"appBundleId":"com.example.app","appName":"com.example.app"}""", result);
    }

    [Fact]
    public async Task Relaunches_an_iOS_app_with_its_launch_arguments()
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.AppsOpen, """{"app":"com.example.app","relaunch":true,"launchArgs":["-reset","1"]}""");

        Assert.Equal(
            ["mobile: terminateApp {\"bundleId\":\"com.example.app\"}", "mobile: launchApp {\"bundleId\":\"com.example.app\",\"arguments\":[\"-reset\",\"1\"]}"],
            Scripts(hub));
    }

    [Fact]
    public async Task Relaunches_an_Android_app_by_package()
    {
        var (client, hub, _) = Appium.Over(MobilePlatform.Android);

        await client.CallAsync(DeviceMethods.AppsOpen, """{"app":"com.example.app","relaunch":true}""");

        Assert.Equal(["mobile: terminateApp {\"appId\":\"com.example.app\"}", "mobile: activateApp {\"appId\":\"com.example.app\"}"], Scripts(hub));
    }

    [Fact]
    public async Task Refuses_launch_arguments_on_Android_before_it_stops_the_app()
    {
        var (client, hub, _) = Appium.Over(MobilePlatform.Android);

        var error = await client.FailureAsync(DeviceMethods.AppsOpen, """{"app":"com.example.app","relaunch":true,"launchArgs":["--es","k","v"]}""");

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.Empty(hub.Calls);
    }

    [Theory]
    [InlineData(MobilePlatform.Ios, "bundleId")]
    [InlineData(MobilePlatform.Android, "package")]
    public async Task Opens_a_link_into_an_app(MobilePlatform platform, string appKey)
    {
        var (client, hub, _) = Appium.Over(platform);

        var result = await client.CallAsync(DeviceMethods.AppsOpen, """{"app":"com.example.app","url":"myapp://orders/42"}""");

        var call = Assert.Single(hub.Scripts());
        Assert.Equal("mobile: deepLink", call.Script);
        Json.AssertEqual($$"""{"url":"myapp://orders/42","{{appKey}}":"com.example.app"}""", call.Arguments);
        Json.AssertEqual("""{"appBundleId":"com.example.app","appName":"com.example.app"}""", result);
    }

    [Fact]
    public async Task A_link_in_place_of_an_app_is_opened_by_the_OS_and_names_no_app()
    {
        var (client, hub, _) = Appium.Over(MobilePlatform.Android);

        var result = await client.CallAsync(DeviceMethods.AppsOpen, """{"app":"https://example.com/verify?t=1"}""");

        Assert.Equal(["mobile: deepLink {\"url\":\"https://example.com/verify?t=1\"}"], Scripts(hub));
        Assert.Null(result);
    }

    [Fact]
    public async Task Opening_needs_an_app_or_a_link()
    {
        var (client, hub, _) = Appium.Over();

        var error = await client.FailureAsync(DeviceMethods.AppsOpen, "{}");

        Assert.Equal("INVALID_ARGS", error.Code);
        Assert.Empty(hub.Calls);
    }

    [Fact]
    public async Task Terminates_an_app_on_close_and_does_nothing_for_none()
    {
        var (client, hub, _) = Appium.Over(MobilePlatform.Android);

        await client.CallAsync(DeviceMethods.AppsClose, """{"app":"com.example.app"}""");
        await client.CallAsync(DeviceMethods.AppsClose, "{}");

        Assert.Equal(["mobile: terminateApp {\"appId\":\"com.example.app\"}"], Scripts(hub));
    }
}
