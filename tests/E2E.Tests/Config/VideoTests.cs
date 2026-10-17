// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Config;

/// <summary>
/// Upstream <c>describe.each(['trace', 'video'])</c> of config.test.ts, for <c>video</c>. The port has no <c>--video</c>
/// flag, so the <c>E2E_VIDEO</c> variable stands in for it.
/// </summary>
public sealed class VideoTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-config-root");

    [Fact]
    public void Defaults_and_a_target_inherits_the_config_mode_as_a_run_wide_one()
    {
        Assert.Equal(new ResolvedRecording(RecordingMode.Off, RecordingSource.Default), E2EConfig.Parse("{}", Root).Target.Video);
        var inherited = E2EConfig.Parse("""{ "video": "retain-on-failure" }""", Root);
        Assert.Equal(new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Run), inherited.Target.Video);
    }

    /// <summary>Partial: the flag is the E2E_VIDEO variable.</summary>
    [Fact]
    public void Lets_a_target_override_the_config_and_E2E_VIDEO_override_both()
    {
        var own = E2EConfig.Parse("""{ "targets": [{ "video": "on-all-retries" }], "video": "on" }""", Root);
        Assert.Equal(new ResolvedRecording(RecordingMode.OnAllRetries, RecordingSource.Target), own.Target.Video);
        var flagged = E2EConfig.Parse("""{ "targets": [{ "video": "off" }], "video": "off" }""", Root, Env(("E2E_VIDEO", "on")));
        Assert.Equal(new ResolvedRecording(RecordingMode.On, RecordingSource.Run), flagged.Target.Video);
        var off = E2EConfig.Parse("""{ "video": "on" }""", Root, Env(("E2E_VIDEO", "off")));
        Assert.Equal(new ResolvedRecording(RecordingMode.Off, RecordingSource.Run), off.Target.Video);
    }

    /// <summary>Partial: the flag is the E2E_VIDEO variable.</summary>
    [Fact]
    public void Refuses_a_mode_it_does_not_know_wherever_it_is_set()
    {
        const string Modes = "off, on, retain-on-failure, on-first-retry, on-all-retries";
        Assert.Equal($"video must be one of {Modes}, got true", Refusal("""{ "video": true }"""));
        Assert.Equal($"targets[0].video must be one of {Modes}, got \"sometimes\"", Refusal("""{ "targets": [{ "video": "sometimes" }] }"""));
        // The variable wins over a target's mode, but never hides a mistake in it.
        Assert.StartsWith("targets[0].video must be one of", Refusal("""{ "targets": [{ "video": "retain_on_failure" }] }""", Env(("E2E_VIDEO", "on"))), StringComparison.Ordinal);
        Assert.StartsWith("E2E_VIDEO must be one of", Refusal("{}", Env(("E2E_VIDEO", "every"))), StringComparison.Ordinal);
    }

    private static string Refusal(string json, Func<string, string?>? environment = null)
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(json, Root, environment ?? Env()));
        Assert.Equal("INVALID_CONFIG", error.Code);
        return error.Message;
    }

    private static Func<string, string?> Env(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        return name => map.GetValueOrDefault(name);
    }
}
