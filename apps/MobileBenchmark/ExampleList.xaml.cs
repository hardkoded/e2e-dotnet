namespace MobileBenchmark;

public partial class ExampleList : ContentPage
{
    public ExampleList()
    {
        InitializeComponent();
    }

    private async void OnControlInventoryClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(ControlInventory));

    private async void OnLoginFormClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(LoginForm));
}
