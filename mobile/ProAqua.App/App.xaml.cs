using ProAqua.App.Services;

namespace ProAqua.App;

public partial class App : Application
{
    public static ProAquaApi Api { get; } = new();

    public App()
    {
        InitializeComponent();
        Api.RestoreSession();

        // Avoid silent black screen on unhandled UI/async errors.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            System.Diagnostics.Debug.WriteLine($"[App] Unhandled: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[App] UnobservedTask: {e.Exception}");
            e.SetObserved();
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new Pages.StartupPage());
    }
}
