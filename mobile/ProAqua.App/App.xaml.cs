using ProAqua.App.Services;

namespace ProAqua.App;

public partial class App : Application
{
    public static ProAquaApi Api { get; } = new();

    public App()
    {
        InitializeComponent();
        Api.RestoreSession();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        if (string.IsNullOrWhiteSpace(Api.Token))
            return new Window(new Pages.LoginPage());

        if (Api.MustChangePassword)
            return new Window(new Pages.ChangePasswordPage(isForced: true));

        return new Window(new AppShell());
    }
}
