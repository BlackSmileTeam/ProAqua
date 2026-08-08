using ProAqua.App.Services;

namespace ProAqua.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        try
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(Pages.LoginPage), typeof(Pages.LoginPage));
            Routing.RegisterRoute(nameof(Pages.BookingPage), typeof(Pages.BookingPage));
            Routing.RegisterRoute(nameof(Pages.ServicesCatalogPage), typeof(Pages.ServicesCatalogPage));
            Routing.RegisterRoute(nameof(Pages.ServiceDetailPage), typeof(Pages.ServiceDetailPage));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] init failed: {ex}");
            throw;
        }
    }
}
