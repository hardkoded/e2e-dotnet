// Copyright 2026 Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static WinAppBenchmark.App.Support;

namespace WinAppBenchmark.Examples;

/// <summary>
/// The deterministic contract surface: plain controls with every state exposed through UI Automation,
/// so each runner verb has an exercise. Not a hard surface. Four sections behind a tab row keep every
/// control on screen without scrolling. The Device section is reduced to what a desktop window can
/// tell the app (its size, the color scheme, and the orientation); the network, location, and
/// biometric lines are mobile-only and are not here.
/// </summary>
public sealed class ControlInventory : Component
{
    private static readonly string[] Sections = ["Fields", "Toggles", "Gestures", "Device"];

    public override Element Render()
    {
        var (section, setSection) = UseState("Fields");
        var scheme = UseColorScheme();
        var (width, height) = UseWindowSize();

        Element body = section switch
        {
            "Toggles" => Component<TogglesSection>(),
            "Gestures" => Component<GesturesSection>(),
            "Device" => VStack(8,
                Body($"{Math.Round(width)}x{Math.Round(height)}").AutomationId("window-size")),
            _ => Component<FieldsSection>(),
        };

        return ScrollView(VStack(8,
            Body("Plain controls, every state exposed").AutomationName("Inventory heading").AutomationId("inventory-header"),
            Body($"scheme: {scheme.ToString().ToLowerInvariant()}").AutomationId("color-scheme"),
            Body($"orientation: {(width > height ? "landscape" : "portrait")}").AutomationId("orientation"),
            HStack(8, [.. Sections.Select(name =>
                Button(name, () => setSection(name))
                    .AutomationName(name)
                    .AutomationId($"tab-{name.ToLowerInvariant()}")
                    .IsEnabled(name != section)
                    .WithKey(name))]).AutomationName("Sections").AutomationId("section-tabs"),
            body)
            .Padding(24))
            .AutomationName("Inventory").AutomationId("inventory-scroll");
    }
}

/// <summary>Text inputs: a labeled field with a placeholder and a default value, a secure field, a field echoing the last key, and two fields whose first autofocuses.</summary>
public sealed class FieldsSection : Component
{
    // The name a key prints as: a letter in lower case, any other key as the platform names it ("Enter").
    private static string KeyName(Windows.System.VirtualKey key) =>
        key is >= Windows.System.VirtualKey.A and <= Windows.System.VirtualKey.Z ? key.ToString().ToLowerInvariant() : key.ToString();

    public override Element Render()
    {
        var (name, setName) = UseState("Ada Lovelace");
        var (passphrase, setPassphrase) = UseState("");
        var (lastKey, setLastKey) = UseState("none");
        var (keyText, setKeyText) = UseState("");
        var (focusShown, setFocusShown) = UseState(false);
        var (focused, setFocused) = UseState("none");
        var (focusRef, requestFocus) = this.UseElementFocus();

        UseEffect(() =>
        {
            if (focusShown)
            {
                requestFocus();
            }
        }, focusShown);

        // Clears the focus status only when the field losing focus still owns it.
        void Blurred(string field) => setFocused(focused == field ? "none" : focused);

        if (focusShown)
        {
            return VStack(8,
                TextBox("", _ => { }).Ref(focusRef).AutomationName("Autofocus field").AutomationId("autofocus-input")
                    .OnGotFocus((_, _) => setFocused("Autofocus field"))
                    .OnLostFocus((_, _) => Blurred("Autofocus field")),
                TextBox("", _ => { }).AutomationName("Second field").AutomationId("second-input")
                    .OnGotFocus((_, _) => setFocused("Second field"))
                    .OnLostFocus((_, _) => Blurred("Second field")),
                Body($"focus: {focused}").AutomationId("focus-status"));
        }

        return VStack(8,
            TextBox(name, setName, placeholderText: "Type your name").AutomationName("Name field").AutomationId("name-input"),
            Body($"name: {name}").AutomationId("name-echo"),
            PasswordBox(passphrase, setPassphrase, placeholderText: "Passphrase").AutomationName("Passphrase field").AutomationId("passphrase-input"),
            Body($"passphrase length: {passphrase.Length}").AutomationId("passphrase-length"),
            TextBox(keyText, setKeyText, placeholderText: "Key echo")
                .AutomationName("Key echo field")
                .AutomationId("key-input")
                .OnKeyDown((_, e) => setLastKey(KeyName(e.Key))),
            Body($"key: {lastKey}").AutomationId("key-status"),
            Button("Show focus fields", () => setFocusShown(true)).AutomationId("show-focus-fields"));
    }
}

/// <summary>Stateful controls: a switch, a checkbox, a radio group, a disabled button, a button that enables itself after mount, and a hidden text with a visible twin.</summary>
public sealed class TogglesSection : Component
{
    private static readonly string[] Sizes = ["Small", "Medium", "Large"];

    public override Element Render()
    {
        var (wifi, setWifi) = UseState(false);
        var (newsletter, setNewsletter) = UseState(false);
        var (size, setSize) = UseState("Small");
        var (delayed, setDelayed) = UseState("arming");

        UseEffect(() =>
        {
            var cts = new CancellationTokenSource();
            After(1500, cts.Token, () => setDelayed("ready"));
            return () => cts.Cancel();
        }, Array.Empty<object>());

        return VStack(8,
            ToggleSwitch(wifi, setWifi, header: "Wi-Fi").AutomationName("Wi-Fi switch").AutomationId("wifi-switch"),
            Body($"wifi: {(wifi ? "on" : "off")}").AutomationId("wifi-status"),
            CheckBox(newsletter, setNewsletter, label: "Newsletter").AutomationName("Newsletter").AutomationId("newsletter-checkbox"),
            Body($"newsletter: {(newsletter ? "on" : "off")}").AutomationId("newsletter-status"),
            HStack(8, [.. Sizes.Select(option =>
                RadioButton(option, size == option, isChecked =>
                {
                    if (isChecked)
                    {
                        setSize(option);
                    }
                }, groupName: "inventory-size")
                .AutomationId($"size-{option.ToLowerInvariant()}")
                .WithKey(option))]).AutomationName("Size").AutomationId("size-group"),
            Body($"size: {size}").AutomationId("size-status"),
            HStack(8,
                Button("Locked", () => { }).IsEnabled(false).AutomationId("locked-button"),
                Button("Delayed", () => setDelayed("pressed")).IsEnabled(delayed != "arming").AutomationId("delayed-button")),
            Body($"delayed: {delayed}").AutomationId("delayed-status"),
            Body("Now you see me").IsVisible(false).AutomationId("ghost-text"),
            Body("Now you see me").AutomationId("twin-text"));
    }
}

/// <summary>Pointer surfaces: a long-press target and a double-tap target that echo the gesture, a pad that reports the tapped point, a pad that reports a swipe's start and end, and a three-row list.</summary>
public sealed class GesturesSection : Component
{
    private const int DoubleTapWindowMilliseconds = 300;

    private static readonly (string Name, string Price)[] Fruits = [("Apple", "$1"), ("Banana", "$2"), ("Cherry", "$3")];

    public override Element Render()
    {
        var (gesture, setGesture) = UseState("none");
        var (taps, setTaps) = UseState("0 taps");
        var (pad, setPad) = UseState("none");
        var (swipe, setSwipe) = UseState("none");
        var lastTap = UseRef(DateTime.MinValue);
        var tapCount = UseRef(0);
        var swipeStart = UseRef(new Windows.Foundation.Point(0, 0));

        // Detects a double tap from consecutive presses and reports the count and the gap, so a pair
        // that missed the window still shows what arrived.
        void DoubleTapCandidate()
        {
            var now = DateTime.UtcNow;
            var gap = (int)(now - lastTap.Current).TotalMilliseconds;
            lastTap.Current = now;
            tapCount.Current += 1;
            setTaps(tapCount.Current == 1 ? "1 tap" : $"{tapCount.Current} taps, {gap} ms apart");
            setGesture(tapCount.Current > 1 && gap <= DoubleTapWindowMilliseconds ? "double tap" : "single tap");
        }

        static string Point(double x, double y) => $"{Math.Round(x)},{Math.Round(y)}";

        return VStack(8,
            HStack(8,
                Button("Hold me", null)
                    .OnLongPress(() => setGesture("long press"), TimeSpan.FromMilliseconds(500), enableMouseEmulation: true)
                    .AutomationId("long-press-target"),
                Button("Double-tap me", DoubleTapCandidate).AutomationId("double-tap-target")),
            Body($"gesture: {gesture}").AutomationId("gesture-status"),
            Body(taps).AutomationId("tap-count"),
            Rectangle().Fill(Brush("#e0e0e0")).Size(240, 100)
                .AutomationName("Tap pad")
                .AutomationId("tap-pad")
                .OnPointerPressed((sender, e) =>
                {
                    var at = e.GetCurrentPoint((UIElement)sender).Position;
                    setPad(Point(at.X, at.Y));
                }),
            Body($"pad: {pad}").AutomationId("pad-status"),
            Rectangle().Fill(Brush("#bbdefb")).Size(240, 100)
                .AutomationName("Swipe pad")
                .AutomationId("swipe-pad")
                .OnPointerPressed((sender, e) => swipeStart.Current = e.GetCurrentPoint((UIElement)sender).Position)
                .OnPointerReleased((sender, e) =>
                {
                    var from = swipeStart.Current;
                    var to = e.GetCurrentPoint((UIElement)sender).Position;
                    setSwipe($"{Point(from.X, from.Y)} to {Point(to.X, to.Y)}");
                }),
            Body($"swipe: {swipe}").AutomationId("swipe-status"),
            VStack(4, [.. Fruits.Select(fruit =>
                HStack(8, Body(fruit.Name), Body(fruit.Price)).AutomationName($"{fruit.Name} row").AutomationId("fruit-row").WithKey(fruit.Name))]).AutomationName("Fruit list").AutomationId("fruit-list"));
    }
}
