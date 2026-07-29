namespace ProAqua.App.Pages;

public partial class AppDialogPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public AppDialogPage(
        string title,
        string message,
        string primaryText = "OK",
        string? secondaryText = null,
        bool isError = false,
        bool centerText = false)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        PrimaryButton.Text = primaryText;
        if (centerText)
        {
            TitleLabel.HorizontalTextAlignment = TextAlignment.Center;
            MessageLabel.HorizontalTextAlignment = TextAlignment.Center;
        }

        if (isError)
        {
            AccentBar.BackgroundColor = Color.FromArgb("#FB7185");
            TitleLabel.TextColor = Color.FromArgb("#FB7185");
        }

        if (!string.IsNullOrWhiteSpace(secondaryText))
        {
            SecondaryButton.IsVisible = true;
            SecondaryButton.Text = secondaryText;
            Grid.SetColumn(PrimaryButton, 1);
        }
        else
        {
            SecondaryButton.IsVisible = false;
            ButtonsGrid.ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(PrimaryButton, 0);
        }
    }

    public Task<bool> Result => _tcs.Task;

    private async void OnPrimary(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(true);
        await CloseAsync();
    }

    private async void OnSecondary(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(false);
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        try
        {
            if (Navigation.ModalStack.Contains(this))
                await Navigation.PopModalAsync(false);
        }
        catch
        {
            // ignore
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (SecondaryButton.IsVisible)
        {
            _ = OnSecondaryInternal();
            return true;
        }
        _ = OnPrimaryInternal();
        return true;
    }

    private async Task OnPrimaryInternal()
    {
        _tcs.TrySetResult(true);
        await CloseAsync();
    }

    private async Task OnSecondaryInternal()
    {
        _tcs.TrySetResult(false);
        await CloseAsync();
    }
}
