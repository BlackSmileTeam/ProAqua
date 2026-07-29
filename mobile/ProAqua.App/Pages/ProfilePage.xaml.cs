using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class ProfilePage : ContentPage
{
    private string _referralCode = string.Empty;
    private double _levelProgress;

    public ProfilePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var profile = await App.Api.GetProfileAsync();
            if (profile is null) return;

            if (profile.MustChangePassword && Application.Current?.Windows.FirstOrDefault() is { } window)
            {
                await Ui.ForceClearBusyAsync();
                window.Page = new ChangePasswordPage(isForced: true);
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(profile.Name) ? "Клиент ПроАква" : profile.Name!;
            NameLabel.Text = displayName;
            PhoneLabel.Text = FormatPhone(profile.Phone);
            LevelLabel.Text = profile.LevelTitle;
            LevelIcon.Source = LevelIconFor(profile.LoyaltyLevel);

            PointsLabel.Text = $"{profile.LoyaltyPoints:N0}".Replace('\u00A0', ' ');

            var (progress, nextText) = LevelProgressInfo(profile.LoyaltyLevel, profile.LoyaltyPoints);
            _levelProgress = progress;
            NextLevelLabel.Text = nextText;
            UpdateLevelProgressFill();

            _referralCode = profile.ReferralCode;
            ReferralCodeLabel.Text = profile.ReferralCode;

            AvatarImage.Source = App.Api.ResolveMediaSource(profile.AvatarUrl, "login_icon.png");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Сессия истекла", StringComparison.OrdinalIgnoreCase))
            {
                App.Api.SetToken(null);
                if (Application.Current?.Windows.FirstOrDefault() is { } window)
                    window.Page = new LoginPage();
                await Ui.ErrorAsync("Сессия истекла. Войдите снова.");
                return;
            }

            await Ui.ErrorAsync(ex.Message);
        }
    }

    private void OnLevelProgressTrackSizeChanged(object? sender, EventArgs e)
        => UpdateLevelProgressFill();

    private void UpdateLevelProgressFill()
    {
        if (LevelProgressTrack.Width <= 0) return;
        LevelProgressFill.WidthRequest = Math.Max(8, LevelProgressTrack.Width * _levelProgress);
    }

    private static string LevelIconFor(int level) => level switch
    {
        >= 3 => "icon_level_platinum.png",
        2 => "icon_level_silver.png",
        _ => "icon_level_guest.png"
    };

    private static (double Progress, string Text) LevelProgressInfo(int level, int points)
    {
        if (level >= 3)
            return (1, "Максимальный уровень — Платина");

        var target = level == 2 ? 2000 : 500;
        var left = Math.Max(0, target - points);
        var progress = Math.Clamp(points / (double)target, 0, 1);
        var text = $"До следующего уровня: {left:N0} баллов".Replace('\u00A0', ' ');
        return (progress, text);
    }

    private static string FormatPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith('7'))
            return $"+7 ({digits[1..4]}) {digits[4..7]}-{digits[7..9]}-{digits[9..11]}";
        return phone;
    }

    private void OnAvatarPointerEntered(object? sender, PointerEventArgs e)
        => AvatarEditHint.IsVisible = true;

    private void OnAvatarPointerExited(object? sender, PointerEventArgs e)
        => AvatarEditHint.IsVisible = false;

    private async void OnAvatarTapped(object? sender, TappedEventArgs e)
    {
        AvatarEditHint.IsVisible = true;
        try
        {
            var action = await DisplayActionSheet("Фото профиля", "Отмена", null, "Выбрать из галереи", "Сделать фото");
            if (action is null or "Отмена") return;

            FileResult? photo = action == "Сделать фото"
                ? await MediaPicker.Default.CapturePhotoAsync()
                : await MediaPicker.Default.PickPhotoAsync();

            if (photo is null) return;

            await using var stream = await photo.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var fileName = photo.FileName;

            var avatarUrl = await Ui.RunBusyAsync(
                () => App.Api.UploadAvatarAsync(bytes, fileName),
                "Загрузка фото…");

            if (string.IsNullOrWhiteSpace(avatarUrl))
                return;

            AvatarImage.Source = App.Api.ResolveMediaSource(avatarUrl, "login_icon.png");
            await Ui.InfoAsync("Готово", "Фото профиля обновлено");
        }
        catch (Exception ex)
        {
            await Ui.ErrorAsync(ex.Message);
        }
        finally
        {
            AvatarEditHint.IsVisible = false;
        }
    }

    private async void OnChangePasswordTapped(object? sender, TappedEventArgs e)
        => await Nav.PushAsync(new ChangePasswordPage(isForced: false));

    private async void OnCopyReferral(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_referralCode)) return;
        await Clipboard.Default.SetTextAsync(_referralCode);
        await Ui.InfoAsync("Скопировано", "Реферальный код скопирован");
    }

    private async void OnLogout(object? sender, TappedEventArgs e)
    {
        var ok = await Ui.ConfirmAsync("Выход", "Выйти из аккаунта?", "Выйти", "Отмена");
        if (!ok) return;
        App.Api.SetToken(null);
        if (Application.Current?.Windows.FirstOrDefault() is { } window)
            window.Page = new LoginPage();
    }
}
