namespace ProAqua.App.Pages;

public partial class ChangePasswordPage : ContentPage
{
    private readonly bool _isForced;

    public ChangePasswordPage(bool isForced = false)
    {
        InitializeComponent();
        _isForced = isForced;
        CancelButton.IsVisible = !isForced;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        ErrorLabel.Text = string.Empty;
        var neu = NewEntry.Text ?? string.Empty;
        if (neu != (ConfirmEntry.Text ?? string.Empty))
        {
            ErrorLabel.Text = "Пароли не совпадают";
            return;
        }

        try
        {
            await App.Api.ChangePasswordAsync(CurrentEntry.Text ?? string.Empty, neu);
            Preferences.Default.Set("must_change_password", false);
            if (_isForced)
            {
                if (Application.Current?.Windows.FirstOrDefault() is { } window)
                    window.Page = new AppShell();
            }
            else if (Navigation.NavigationStack.Count > 1)
            {
                await DisplayAlert("Готово", "Пароль обновлён", "OK");
                await Navigation.PopAsync();
            }
            else if (Application.Current?.Windows.FirstOrDefault() is { } window)
            {
                window.Page = new AppShell();
            }
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_isForced) return true;
        return base.OnBackButtonPressed();
    }
}
