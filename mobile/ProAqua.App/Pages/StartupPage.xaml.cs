namespace ProAqua.App.Pages;

public partial class StartupPage : ContentPage
{
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

            var profile = await App.Api.GetProfileAsync();
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
        catch
        {
            App.Api.SetToken(null);
            window.Page = new LoginPage();
        }
    }
}
