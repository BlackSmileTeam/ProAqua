namespace ProAqua.App.Pages;

public partial class BusyOverlayPage : ContentPage
{
    public BusyOverlayPage(string? message = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(message))
            MessageLabel.Text = message;
    }

    protected override bool OnBackButtonPressed() => true;
}
