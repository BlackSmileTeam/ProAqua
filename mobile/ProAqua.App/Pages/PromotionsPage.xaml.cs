using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class PromotionsPage : ContentPage
{
    public PromotionsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnRefresh(object? sender, EventArgs e)
    {
        await LoadAsync();
        Refresh.IsRefreshing = false;
    }

    private async Task LoadAsync()
    {
        try
        {
            var promos = await App.Api.GetPromotionsAsync() ?? [];
            var cards = promos.Select(p =>
            {
                var imageUrl = ProAquaApi.AbsoluteMediaUrl(p.ImageUrl);
                return new PromotionCard(
                    p.Title,
                    p.Description,
                    FormatPeriod(p.StartsAt, p.EndsAt),
                    imageUrl,
                    !string.IsNullOrWhiteSpace(imageUrl));
            }).ToList();
            EmptyLabel.IsVisible = cards.Count == 0;
            PromotionsList.ItemsSource = cards;
        }
        catch (Exception ex)
        {
            EmptyLabel.IsVisible = true;
            PromotionsList.ItemsSource = Array.Empty<PromotionCard>();
            await Ui.ErrorAsync(ex.Message);
        }
    }

    private static string FormatPeriod(DateTime startsAt, DateTime endsAt)
    {
        var from = startsAt.ToLocalTime().ToString("dd.MM.yyyy");
        var to = endsAt.ToLocalTime().ToString("dd.MM.yyyy");
        return $"Действует: {from} — {to}";
    }

    private sealed record PromotionCard(string Title, string Description, string PeriodText, string? ImageUrl, bool HasImage);
}
