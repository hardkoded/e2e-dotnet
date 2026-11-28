using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace MobileBenchmark;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density
)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "e2e-benchmark"
)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (savedInstanceState is null)
        {
            ForwardLink(Intent);
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        ForwardLink(intent);
    }

    /// <summary>Hands a <c>e2e-benchmark://</c> link to <c>App.OnAppLinkRequestReceived</c>, once the shell is up.</summary>
    private static void ForwardLink(Intent? intent)
    {
        if (
            intent?.Action != Intent.ActionView
            || !Uri.TryCreate(intent.Data?.ToString(), UriKind.Absolute, out var link)
        )
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
            Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(link)
        );
    }
}
