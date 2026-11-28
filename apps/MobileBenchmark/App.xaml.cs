using Microsoft.Extensions.DependencyInjection;

namespace MobileBenchmark;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    /// <summary>
    /// Routes <c>e2e-benchmark://&lt;scenario-slug&gt;</c> to that scenario, as upstream's <c>linking</c> does in <c>src/App.tsx</c>.
    /// </summary>
    protected override async void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        if (uri.Scheme != "e2e-benchmark")
        {
            return;
        }

        var route = uri.Host switch
        {
            "login-form" => nameof(LoginForm),
            "control-inventory" => nameof(ControlInventory),
            _ => null,
        };
        if (route is not null && Shell.Current is { } shell)
        {
            await shell.GoToAsync(route);
        }
    }
}
