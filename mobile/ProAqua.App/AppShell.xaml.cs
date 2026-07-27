namespace ProAqua.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(Pages.LoginPage), typeof(Pages.LoginPage));
        Routing.RegisterRoute(nameof(Pages.BookingPage), typeof(Pages.BookingPage));
    }
}
