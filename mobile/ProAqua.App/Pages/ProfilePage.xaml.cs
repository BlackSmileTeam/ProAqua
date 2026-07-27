namespace ProAqua.App.Pages;

public partial class ProfilePage : ContentPage
{
    private string _referralCode = string.Empty;

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
                window.Page = new ChangePasswordPage(isForced: true);
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(profile.Name) ? "Клиент ПроАква" : profile.Name!;
            NameLabel.Text = displayName;
            PhoneLabel.Text = FormatPhone(profile.Phone);
            NameEntry.Text = profile.Name ?? string.Empty;
            LevelLabel.Text = profile.LevelTitle;

            var pointsText = profile.LoyaltyPoints.ToString("N0").Replace('\u00A0', ' ');
            PointsLabel.Text = pointsText;
            PointsBadgeLabel.Text = pointsText;

            var (progress, nextText) = LevelProgressInfo(profile.LoyaltyLevel, profile.LoyaltyPoints);
            LevelProgress.Progress = progress;
            NextLevelLabel.Text = nextText;

            _referralCode = profile.ReferralCode;
            ReferralCodeLabel.Text = profile.ReferralCode;
            ReferralCountLabel.Text = profile.ReferralCount == 0
                ? "Пока никто не зарегистрировался по вашему коду"
                : $"Приглашено друзей: {profile.ReferralCount}";

            AvatarImage.Source = App.Api.ResolveMediaSource(profile.AvatarUrl, "login_icon.png");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    private static (double Progress, string Text) LevelProgressInfo(int level, int points)
    {
        if (level >= 3)
            return (1, "Максимальный уровень — Платина");

        var target = level == 2 ? 5000 : 1000;
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

    private async void OnAvatarTapped(object? sender, TappedEventArgs e)
    {
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
            var avatarUrl = await App.Api.UploadAvatarAsync(ms.ToArray(), photo.FileName);
            AvatarImage.Source = App.Api.ResolveMediaSource(avatarUrl, "login_icon.png");
            await DisplayAlert("Готово", "Фото профиля обновлено", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    private async void OnSaveName(object? sender, EventArgs e)
    {
        try
        {
            await App.Api.UpdateProfileAsync(NameEntry.Text?.Trim());
            await DisplayAlert("Готово", "Имя обновлено", "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    private async void OnChangePasswordTapped(object? sender, TappedEventArgs e)
        => await Navigation.PushAsync(new ChangePasswordPage(isForced: false));

    private async void OnCopyReferral(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_referralCode)) return;
        await Clipboard.Default.SetTextAsync(_referralCode);
        await DisplayAlert("Скопировано", "Реферальный код скопирован", "OK");
    }

    private void OnLogout(object? sender, EventArgs e)
    {
        App.Api.SetToken(null);
        if (Application.Current?.Windows.FirstOrDefault() is { } window)
            window.Page = new LoginPage();
    }
}
