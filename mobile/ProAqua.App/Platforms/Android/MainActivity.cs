using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace ProAqua.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Window is null) return;

        var navy = Android.Graphics.Color.ParseColor("#07151C");
        Window.SetStatusBarColor(navy);
        Window.SetNavigationBarColor(navy);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            Window.SetDecorFitsSystemWindows(true);
            var controller = Window.InsetsController;
            if (controller is not null)
            {
                controller.SetSystemBarsAppearance(0,
                    (int)WindowInsetsControllerAppearance.LightStatusBars |
                    (int)WindowInsetsControllerAppearance.LightNavigationBars);
            }
        }
        else
        {
#pragma warning disable CS0618
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.Visible;
#pragma warning restore CS0618
        }
    }
}
