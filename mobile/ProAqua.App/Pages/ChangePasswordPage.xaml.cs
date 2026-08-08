using ProAqua.App.Services;

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
        await Nav.PopAsync();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        ErrorLabel.Text = string.Empty;
        var neu = NewEntry.Text ?? string.Empty;
        if (neu != (ConfirmEntry.Text ?? string.Empty))
        {
            await Ui.ErrorAsync("Пароли не совпадают");
            return;
        }

        try
        {
            await Ui.RunBusyAsync(
                () => App.Api.ChangePasswordAsync(CurrentEntry.Text ?? string.Empty, neu),
                "Сохраняем…");

            Preferences.Default.Set("must_change_password", false);
            await Ui.ForceClearBusyAsync();
            await Task.Delay(50);
            if (_isForced)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (Application.Current?.Windows.FirstOrDefault() is { } window)
                        window.Page = new AppShell();
                });
            }
            else if (Nav.CanPop)
            {
                await Ui.InfoAsync("Готово", "Пароль обновлён");
                await Nav.PopAsync();
            }
            else if (Application.Current?.Windows.FirstOrDefault() is { } window)
            {
                await MainThread.InvokeOnMainThreadAsync(() => window.Page = new AppShell());
            }
        }
        catch (Exception ex)
        {
            await Ui.ErrorAsync(ex.Message);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_isForced) return true;
        return base.OnBackButtonPressed();
    }
}
