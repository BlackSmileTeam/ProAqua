using System.Diagnostics;
using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class LoginPage : ContentPage
{
    private bool _passwordVisible;
    private bool _loginInProgress;
    private CancellationTokenSource? _loginCts;

    public LoginPage(string? connectionHint = null)
    {
        InitializeComponent();
        PhoneEntry.Text = "+79";
        if (!string.IsNullOrWhiteSpace(connectionHint))
            ErrorLabel.Text = connectionHint;
    }

    private void TraceLog(string message)
    {
        Debug.WriteLine($"[Login] {message}");
#if ANDROID
        Android.Util.Log.Info("ProAqua", $"[Login] {message}");
#endif
    }

    private void OnTogglePassword(object? sender, EventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        PasswordEntry.IsPassword = !_passwordVisible;
        TogglePasswordButton.Text = _passwordVisible ? "Скрыть" : "Показать";
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _loginInProgress = busy;
        BusyOverlay.IsVisible = busy;
        BusySpinner.IsRunning = busy;
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

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            ErrorLabel.Text = "Нет интернета на устройстве";
            await DisplayAlertSafeAsync("Вход", ErrorLabel.Text);
            return;
        }

        _loginCts?.Cancel();
        _loginCts?.Dispose();
        _loginCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = _loginCts.Token;

        SetBusy(true, "Вход…");
        // Даём UI-потоку отрисовать оверлей до сети (иначе Android ANR без спиннера).
        await Task.Yield();
        await Task.Delay(50);

        TraceLog($"start phone={phone} api={ProAquaApi.BaseUrl}");

        try
        {
            // Весь HTTP строго вне UI-потока.
            var auth = await Task.Run(
                async () => await App.Api.LoginAsync(phone, password, ct).ConfigureAwait(false),
                ct).ConfigureAwait(true);

            TraceLog($"ok userId={auth.UserId} mustChange={auth.MustChangePassword}");
            SetBusy(true, "Открываем…");
            await Task.Delay(30);

            var mustChange = auth.MustChangePassword;
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
        catch (OperationCanceledException)
        {
            TraceLog("canceled/timeout");
            SetBusy(false);
            ErrorLabel.Text = "Сервер не отвечает. Проверьте интернет и повторите.";
            await DisplayAlertSafeAsync("Вход", ErrorLabel.Text);
        }
        catch (Exception ex)
        {
            TraceLog($"error: {ex}");
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

    protected override void OnDisappearing()
    {
        _loginCts?.Cancel();
        base.OnDisappearing();
    }
}
