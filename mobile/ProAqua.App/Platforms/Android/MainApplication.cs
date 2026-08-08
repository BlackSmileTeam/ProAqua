using Android.App;
using Android.Runtime;
using Android.Util;

namespace ProAqua.App;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
		{
			Log.Error("ProAqua", $"Unhandled: {args.Exception}");
			// Keep default crash behavior, but leave a logcat breadcrumb.
			args.Handled = false;
		};
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
