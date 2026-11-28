namespace MobileBenchmark;

/// <summary>Upstream's Login Form scenario: the hardcoded benchmark account moves the screen into its logged-in state.</summary>
public partial class LoginForm : ContentPage
{
    private const string ValidEmail = "tester@tester.army";
    private const string ValidPassword = "benchmark123";

    public LoginForm()
    {
        InitializeComponent();
    }

    private void OnSubmit(object? sender, EventArgs e)
    {
        var email = Email.Text ?? "";
        var password = Password.Text ?? "";
        var error =
            !email.Contains('@') ? "Enter a valid email address"
            : password.Length < 8 ? "Password must be at least 8 characters"
            : email != ValidEmail || password != ValidPassword ? "Invalid credentials"
            : null;
        Error.Text = error;
        Error.IsVisible = error is not null;
        if (error is not null)
        {
            return;
        }

        FormSection.IsVisible = false;
        SuccessSection.IsVisible = true;
    }

    private void OnLogout(object? sender, EventArgs e)
    {
        Email.Text = "";
        Password.Text = "";
        SuccessSection.IsVisible = false;
        FormSection.IsVisible = true;
    }
}
