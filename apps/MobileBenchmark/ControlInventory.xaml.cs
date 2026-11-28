using System.Diagnostics;

namespace MobileBenchmark;

/// <summary>
/// Upstream's Control Inventory scenario: the Fields section's name field, the Gestures section (a long-press target and a double-tap target that echo the
/// gesture, and the count of taps that arrived with the gap between the last two) and the Device section (the system color scheme, the orientation, the network state,
/// a position read on demand, and the outcome of a biometric prompt).
/// </summary>
public partial class ControlInventory : ContentPage
{
    private const int DoubleTapWindowMilliseconds = 300;
    private const int LongPressMilliseconds = 500;

    private readonly Stopwatch _held = new();
    private long _lastTapTicks = long.MinValue;
    private int _tapCount;

    public ControlInventory()
    {
        InitializeComponent();
        Loaded += (_, _) => Subscribe();
        Unloaded += (_, _) => Unsubscribe();
    }

    private void OnFieldsTabClicked(object? sender, EventArgs e) => ShowSection(FieldsSection);

    private void OnGesturesTabClicked(object? sender, EventArgs e) => ShowSection(GesturesSection);

    private void OnDeviceTabClicked(object? sender, EventArgs e)
    {
        ShowSection(DeviceSection);
        ShowNetwork();
    }

    private void OnNameChanged(object? sender, TextChangedEventArgs e) =>
        NameEcho.Text = string.IsNullOrEmpty(e.NewTextValue) ? "name:" : $"name: {e.NewTextValue}";

    /// <summary>Shows one of the scenario's sections and hides the others.</summary>
    private void ShowSection(View section)
    {
        FieldsSection.IsVisible = section == FieldsSection;
        GesturesSection.IsVisible = section == GesturesSection;
        DeviceSection.IsVisible = section == DeviceSection;
    }

    private void Subscribe()
    {
        ShowColorScheme();
        ShowOrientation();
        Unsubscribe();
        if (Application.Current is { } app)
        {
            app.RequestedThemeChanged += OnThemeChanged;
        }

        DeviceDisplay.Current.MainDisplayInfoChanged += OnDisplayInfoChanged;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private void Unsubscribe()
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeChanged -= OnThemeChanged;
        }

        DeviceDisplay.Current.MainDisplayInfoChanged -= OnDisplayInfoChanged;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(ShowColorScheme);

    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(ShowOrientation);

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(ShowNetwork);

    private void ShowColorScheme() =>
        ColorScheme.Text = Application.Current!.RequestedTheme == AppTheme.Dark ? "scheme: dark" : "scheme: light";

    private void ShowOrientation() =>
        Orientation.Text =
            DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Landscape
                ? "orientation: landscape"
                : "orientation: portrait";

    private void ShowNetwork()
    {
        var connectivity = Connectivity.Current;
        NetworkState.Text =
            connectivity.NetworkAccess == NetworkAccess.Internet || connectivity.NetworkAccess == NetworkAccess.Local
                ? "network: connected"
                : "network: disconnected";
        var profiles = connectivity.ConnectionProfiles;
        NetworkType.Text =
            profiles.Contains(ConnectionProfile.WiFi) ? "network type: wifi"
            : profiles.Contains(ConnectionProfile.Cellular) ? "network type: cellular"
            : "network type: none";
    }

    /// <summary>A position read on demand: the provider's last fix, or the status the read failed with.</summary>
    private async void OnReadLocationClicked(object? sender, EventArgs e)
    {
        LocationStatus.Text = "location: reading";
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status is not (PermissionStatus.Granted or PermissionStatus.Restricted))
            {
                LocationStatus.Text = "location: permission denied";
                return;
            }

            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10))
            );
            LocationStatus.Text = location is null
                ? "location: unavailable"
                : string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"location: {location.Latitude:0.0000}, {location.Longitude:0.0000}"
                );
        }
        catch (Exception error)
        {
            LocationStatus.Text = $"location: unavailable ({error.GetType().Name})";
        }
    }

    private async void OnUnlockBiometricsClicked(object? sender, EventArgs e)
    {
        BiometricsStatus.Text = "biometrics: authenticating";
        try
        {
            BiometricsStatus.Text = await Biometrics.AuthenticateAsync()
                ? "biometrics: unlocked"
                : "biometrics: failed";
        }
        catch (Exception)
        {
            BiometricsStatus.Text = "biometrics: failed";
        }
    }

    private void OnHoldMePressed(object? sender, EventArgs e) => _held.Restart();

    private void OnHoldMeReleased(object? sender, EventArgs e) =>
        GestureStatus.Text =
            _held.ElapsedMilliseconds >= LongPressMilliseconds ? "gesture: long press" : "gesture: short press";

    /// <summary>Detects a double tap from consecutive presses and reports the count and the gap, so a pair that missed the window still shows what arrived.</summary>
    private void OnDoubleTapCandidate(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var gap = _tapCount == 0 ? 0 : (int)Stopwatch.GetElapsedTime(_lastTapTicks, now).TotalMilliseconds;
        _lastTapTicks = now;
        _tapCount++;
        TapCount.Text = _tapCount == 1 ? "1 tap" : $"{_tapCount} taps, {gap} ms apart";
        GestureStatus.Text =
            _tapCount > 1 && gap <= DoubleTapWindowMilliseconds ? "gesture: double tap" : "gesture: single tap";
    }
}
