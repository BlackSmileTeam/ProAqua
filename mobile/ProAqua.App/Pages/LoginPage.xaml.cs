namespace ProAqua.App.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        PhoneEntry.Text = "+79";
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        ErrorLabel.Text = string.Empty;
        try
        {
            var auth = await App.Api.LoginAsync(PhoneEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty);
            if (Application.Current?.Windows.FirstOrDefault() is not { } window)
                return;

            window.Page = auth.MustChangePassword
                ? new ChangePasswordPage(isForced: true)
                : new AppShell();
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
        }
    }
}
