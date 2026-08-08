using Microsoft.Extensions.Logging;

namespace ProAqua.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("ProAquaEntryAccent", (handler, _) =>
		{
#if ANDROID
			var gold = Android.Graphics.Color.ParseColor("#D4AF37");
			handler.PlatformView.BackgroundTintList =
				Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
			handler.PlatformView.TextCursorDrawable?.SetTint(gold);
			handler.PlatformView.SetHighlightColor(Android.Graphics.Color.Argb(0x55, 0xD4, 0xAF, 0x37));
			handler.PlatformView.SetLinkTextColor(gold);
#elif IOS || MACCATALYST
			handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
			handler.PlatformView.TintColor = UIKit.UIColor.FromRGB(0xD4, 0xAF, 0x37);
#elif WINDOWS
			handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
			var goldBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
				Windows.UI.Color.FromArgb(255, 0xD4, 0xAF, 0x37));
			handler.PlatformView.SelectionHighlightColor = goldBrush;
#endif
		});

		Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("ProAquaEditorAccent", (handler, _) =>
		{
#if ANDROID
			var gold = Android.Graphics.Color.ParseColor("#D4AF37");
			handler.PlatformView.BackgroundTintList =
				Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
			handler.PlatformView.TextCursorDrawable?.SetTint(gold);
			handler.PlatformView.SetHighlightColor(Android.Graphics.Color.Argb(0x55, 0xD4, 0xAF, 0x37));
#elif IOS || MACCATALYST
			handler.PlatformView.TintColor = UIKit.UIColor.FromRGB(0xD4, 0xAF, 0x37);
#elif WINDOWS
			var goldBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
				Windows.UI.Color.FromArgb(255, 0xD4, 0xAF, 0x37));
			handler.PlatformView.SelectionHighlightColor = goldBrush;
#endif
		});

		Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("ProAquaDatePickerAccent", (handler, _) =>
		{
#if ANDROID
			handler.PlatformView.BackgroundTintList =
				Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
			handler.PlatformView.SetTextColor(Android.Graphics.Color.White);
#elif IOS || MACCATALYST
			handler.PlatformView.TintColor = UIKit.UIColor.FromRGB(0xD4, 0xAF, 0x37);
#endif
		});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
