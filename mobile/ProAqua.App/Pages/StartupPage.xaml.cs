using System.Net.Http;
using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class StartupPage : ContentPage
{
    private const int SessionCheckTimeoutSeconds = 12;
    private bool _initialized;

    public StartupPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;

        if (Application.Current?.Windows.FirstOrDefault() is not { } window)
            return;

        try
        {
            if (string.IsNullOrWhiteSpace(App.Api.Token))
            {
                window.Page = new LoginPage();
                return;
            }

            Profile? profile;
            try
            {
                profile = await App.Api.GetProfileAsync()
                    .WaitAsync(TimeSpan.FromSeconds(SessionCheckTimeoutSeconds));
            }
            catch (TimeoutException)
            {
                // Keep token — API may be temporarily down; show login with connection hint.
                window.Page = new LoginPage(connectionHint: "Нет связи");
                return;
            }
            catch (Exception ex) when (IsConnectivityFailure(ex))
            {
                window.Page = new LoginPage(connectionHint: "Нет связи");
                return;
            }

            if (profile is null)
            {
                App.Api.SetToken(null);
                window.Page = new LoginPage();
                return;
            }

            if (profile.MustChangePassword)
            {
                window.Page = new ChangePasswordPage(isForced: true);
                return;
            }

            window.Page = new AppShell();
        }
        catch (Exception ex)
        {
            // Never leave a blank/stuck splash: always land on login.
            System.Diagnostics.Debug.WriteLine($"[Startup] {ex}");
            var hint = IsConnectivityFailure(ex) ? "Нет связи" : null;
            if (hint is null)
                App.Api.SetToken(null);
            window.Page = new LoginPage(connectionHint: hint);
        }
    }

    private static bool IsConnectivityFailure(Exception ex)
    {
        if (ex is HttpRequestException or TaskCanceledException or TimeoutException)
            return true;
        return ex.Message?.Contains("Нет связи", StringComparison.Ordinal) == true;
    }
}
