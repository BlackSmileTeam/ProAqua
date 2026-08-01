using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(string? connectionHint = null)
    {
        InitializeComponent();
        PhoneEntry.Text = "+79";
        if (!string.IsNullOrWhiteSpace(connectionHint))
            ErrorLabel.Text = connectionHint;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        ErrorLabel.Text = string.Empty;
        try
        {
            var auth = await Ui.RunBusyAsync(
                () => App.Api.LoginAsync(PhoneEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty),
                "Вход…");

            if (auth is null) return;
            if (Application.Current?.Windows.FirstOrDefault() is not { } window)
                return;

            // Важно: снять оверлей ДО замены корня окна, иначе остаётся «блюр»
            await Ui.ForceClearBusyAsync();

            window.Page = auth.MustChangePassword
                ? new ChangePasswordPage(isForced: true)
                : new AppShell();
        }
        catch (Exception ex)
        {
            await Ui.ForceClearBusyAsync();
            var msg = string.IsNullOrWhiteSpace(ex.Message) ? "Нет связи" : ex.Message;
            ErrorLabel.Text = msg;
            await Ui.ErrorAsync(msg);
        }
    }
}
