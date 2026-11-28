namespace MobileBenchmark;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(ControlInventory), typeof(ControlInventory));
        Routing.RegisterRoute(nameof(LoginForm), typeof(LoginForm));
    }
}
