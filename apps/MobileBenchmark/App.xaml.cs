using Microsoft.Extensions.DependencyInjection;

namespace MobileBenchmark;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    private Uri? _pendingLink;
    private bool _shellLoaded;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = new AppShell();
        // A link that arrives during a cold start finds no shell yet; it is followed once the shell is up.
        shell.Loaded += (_, _) =>
        {
            _shellLoaded = true;
            _ = FollowPendingLinkAsync();
        };
        return new Window(shell);
    }

    /// <summary>
    /// Routes <c>e2e-benchmark://&lt;scenario-slug&gt;</c> to that scenario, as upstream's <c>linking</c> does in <c>src/App.tsx</c>.
    /// The link is kept until the shell is ready, so a cold start follows it too.
    /// </summary>
    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        if (uri.Scheme != "e2e-benchmark")
        {
            return;
        }

        _pendingLink = uri;
        _ = MainThread.InvokeOnMainThreadAsync(FollowPendingLinkAsync);
    }

    private async Task FollowPendingLinkAsync()
    {
        if (!_shellLoaded || _pendingLink is not { } uri || Shell.Current is not { } shell)
        {
            return;
        }

        var route = uri.Host switch
        {
            "login-form" => nameof(LoginForm),
            "control-inventory" => nameof(ControlInventory),
            _ => null,
        };
        if (route is null)
        {
            _pendingLink = null;
            return;
        }

        try
        {
            await shell.GoToAsync(route);
            _pendingLink = null;
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"e2e-benchmark link {uri} was not followed: {error}");
        }
    }
}
