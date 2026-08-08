using System.Diagnostics;
using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class LoginPage : ContentPage
{
    private bool _passwordVisible;
    private bool _loginInProgress;

    public LoginPage(string? connectionHint = null)
    {
        InitializeComponent();
        PhoneEntry.Text = "+79";
        if (!string.IsNullOrWhiteSpace(connectionHint))
            ErrorLabel.Text = connectionHint;
    }

    private void OnTogglePassword(object? sender, EventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        PasswordEntry.IsPassword = !_passwordVisible;
        TogglePasswordButton.Source = _passwordVisible ? "icon_eye_open.png" : "icon_eye_off.png";
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _loginInProgress = busy;
        BusyOverlay.IsVisible = busy;
        LoginButton.IsEnabled = !busy;
        PhoneEntry.IsEnabled = !busy;
        PasswordEntry.IsEnabled = !busy;
        TogglePasswordButton.IsEnabled = !busy;
        if (!string.IsNullOrWhiteSpace(message))
            BusyLabel.Text = message;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        if (_loginInProgress)
            return;

        ErrorLabel.Text = string.Empty;
        var phone = PhoneEntry.Text ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
        {
            ErrorLabel.Text = "Введите телефон и пароль";
            return;
        }

        SetBusy(true, "Вход…");
        Debug.WriteLine($"[Login] start phone={phone} api={ProAquaApi.BaseUrl}");

        try
        {
            // HTTP на фоне (не блокируем UI); таймаут на случай «вечного» ожидания.
            var auth = await App.Api.LoginAsync(phone, password)
                .WaitAsync(TimeSpan.FromSeconds(25));

            Debug.WriteLine($"[Login] ok userId={auth.UserId} mustChange={auth.MustChangePassword}");
            SetBusy(true, "Открываем…");

            // Не вызываем Ui.ForceClearBusyAsync — на Android возможен deadlock на UI-потоке.
            // Страницы MAUI создаём только на UI-потоке.
            var mustChange = auth.MustChangePassword;
            Application.Current?.Dispatcher.Dispatch(() =>
            {
                try
                {
                    var window = Application.Current?.Windows.FirstOrDefault();
                    if (window is null)
                    {
                        SetBusy(false);
                        ErrorLabel.Text = "Окно приложения недоступно";
                        return;
                    }

                    window.Page = mustChange
                        ? (Page)new ChangePasswordPage(isForced: true)
                        : new AppShell();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Login] set page failed: {ex}");
                    SetBusy(false);
                    ErrorLabel.Text = ex.Message;
                }
            });
        }
        catch (TimeoutException)
        {
            Debug.WriteLine("[Login] timeout");
            SetBusy(false);
            ErrorLabel.Text = "Сервер не отвечает. Проверьте интернет и повторите.";
            await DisplayAlertSafeAsync("Вход", ErrorLabel.Text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Login] error: {ex}");
            SetBusy(false);
            var msg = string.IsNullOrWhiteSpace(ex.Message) ? "Не удалось войти" : ex.Message;
            ErrorLabel.Text = msg;
            await DisplayAlertSafeAsync("Вход", msg);
        }
    }

    private async Task DisplayAlertSafeAsync(string title, string message)
    {
        try { await DisplayAlert(title, message, "OK"); }
        catch { /* page may be gone */ }
    }
}
