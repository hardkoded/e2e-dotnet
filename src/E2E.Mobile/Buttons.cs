// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile;

/// <summary>A hardware button <see cref="Device.PressButtonAsync"/> takes. Upstream's <c>HardwareButton</c>.</summary>
public enum HardwareButton
{
    VolumeUp,
    VolumeDown,
    Power,
}

/// <summary>A phone's hardware buttons, pressed through agent-device's <c>hardware-button</c>. Upstream's <c>buttons.ts</c>.</summary>
internal static class Buttons
{
    /// <summary>
    /// Every hardware button, in the order the docs list them. agent-device sends them as key events on an Android emulator or
    /// phone, and the volume buttons through the XCUITest runner on a physical iPhone or iPad. An iOS simulator, an Android TV, and
    /// an iPhone's or iPad's power button are refused, which reaches the test as <c>UNSUPPORTED_CAPABILITY</c>. agent-device also
    /// has <c>mute</c>, <c>sleep</c>, <c>wake</c>, and media keys; they are left out until a phone test needs them.
    /// Upstream's <c>HARDWARE_BUTTONS</c>.
    /// </summary>
    private static readonly HardwareButton[] HardwareButtons =
    [
        HardwareButton.VolumeUp,
        HardwareButton.VolumeDown,
        HardwareButton.Power,
    ];

    /// <summary>The name agent-device and the docs know a hardware button by.</summary>
    public static string WireName(this HardwareButton button) =>
        button switch
        {
            HardwareButton.VolumeUp => "volume-up",
            HardwareButton.VolumeDown => "volume-down",
            HardwareButton.Power => "power",
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown hardware button."),
        };

    /// <summary>
    /// Presses one hardware button on the selected device. The press takes the input budget's signal, so when the budget runs
    /// out it cancels a request the daemon has not sent yet, instead of letting a late <c>power</c> turn the screen off.
    /// Upstream's <c>pressButton</c>.
    /// </summary>
    public static Task<JsonNode?> PressButtonAsync(
        IDeviceClient client,
        HardwareButton button,
        DeviceSelection selection,
        CancellationToken signal
    ) =>
        client.CallAsync(
            DeviceMethods.CommandHardwareButton,
            selection.ToArgs(("button", JsonValue.Create(button.WireName()))),
            signal
        );

    /// <summary>Throws <c>INVALID_ARGUMENT</c> unless <paramref name="button"/> is one of the hardware buttons. Upstream's <c>requireHardwareButton</c>.</summary>
    public static void RequireHardwareButton(HardwareButton button)
    {
        if (Array.IndexOf(HardwareButtons, button) >= 0)
        {
            return;
        }

        throw new TestException(
            "INVALID_ARGUMENT",
            $"device.pressButton({(int)button}): not a hardware button; press one of {string.Join(", ", HardwareButtons.Select(WireName))}"
        );
    }
}
