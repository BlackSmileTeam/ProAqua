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
        return new Window(new Pages.StartupPage());
    }
}
