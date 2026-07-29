using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class HomePage : ContentPage
{
    private const double PromoSpacing = 12;
    private const int PromoCount = 5;

    private List<ServiceCard> _services = [];

    public HomePage()
    {
        InitializeComponent();
        BuildPromoDashes(0);
    }

    private void OnPromoScrolled(object? sender, ScrolledEventArgs e)
    {
        var slideWidth = PromoScroll.Width;
        if (slideWidth <= 0)
            return;

        var index = (int)Math.Round(e.ScrollX / (slideWidth + PromoSpacing));
        BuildPromoDashes(Math.Clamp(index, 0, PromoCount - 1));
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
            var items = await App.Api.GetServicesAsync() ?? [];
            _services = items.Select(MapCard).ToList();

            var preview = _services
                .Take(3)
                .Select(s => PreviewCard.FromService(s))
                .ToList();
            preview.Add(PreviewCard.More());
            BindableLayout.SetItemsSource(ServicesRow, preview);

            await LoadUpcomingBookingAsync();
        }
        catch (Exception ex)
        {
            await Ui.ErrorAsync(ex.Message);
        }
    }

    private async Task LoadUpcomingBookingAsync()
    {
        try
        {
            var bookings = await App.Api.GetMyBookingsAsync() ?? [];
            var next = bookings
                .Select(BookingListItem.From)
                .Where(b => b.IsUpcoming)
                .OrderBy(b => b.StartAtUtc)
                .FirstOrDefault();

            if (next is null)
            {
                UpcomingBookingCard.IsVisible = false;
                return;
            }

            UpcomingBookingCard.IsVisible = true;
            UpcomingServiceLabel.Text = next.ServiceTitle;
            UpcomingWhenLabel.Text = next.DateTimeText;
            UpcomingStatusLabel.Text = $"Статус: {next.StatusRu}";
        }
        catch
        {
            UpcomingBookingCard.IsVisible = false;
        }
    }

    private void BuildPromoDashes(int activeIndex)
    {
        PromoDashIndicators.Children.Clear();
        for (var i = 0; i < PromoCount; i++)
        {
            var isActive = i == activeIndex;
            PromoDashIndicators.Children.Add(new BoxView
            {
                Color = isActive ? Color.FromArgb("#26C6DA") : Color.FromArgb("#6B7280"),
                HeightRequest = 5,
                WidthRequest = isActive ? 30 : 12,
                CornerRadius = 3,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center
            });
        }
    }

    private async void OnOpenBookings(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//history");

    private async void OnOpenServicesCatalog(object? sender, TappedEventArgs e)
        => await Nav.PushAsync(new ServicesCatalogPage());

    private static ServiceCard MapCard(ServiceItem s)
    {
        ImageSource image = !string.IsNullOrWhiteSpace(s.ImageUrl)
            ? ImageSource.FromUri(new Uri(s.ImageUrl))
            : FallbackLocalImage(s.Title);

        return new ServiceCard(s.Id, s.Title, s.Description, s.PriceFrom, s.DurationMinutes, image, s);
    }

    private static ImageSource FallbackLocalImage(string title)
    {
        var t = title.ToLowerInvariant();
        var file = t switch
        {
            var x when x.Contains("глубок") || x.Contains("полир") => "service_deep_clean.png",
            var x when x.Contains("керами") || x.Contains("ppf") || x.Contains("плён") || x.Contains("плен") => "service_ceramic.png",
            var x when x.Contains("детейл") => "service_detailing.png",
            var x when x.Contains("химчист") || x.Contains("салон") || x.Contains("интерьер") || x.Contains("комплекс") => "service_interior.png",
            _ => "service_wash.png"
        };
        return ImageSource.FromFile(file);
    }

    private async void OnPreviewCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not PreviewCard card)
            return;

        if (card.IsMore)
        {
            await Ui.ShowBusyAsync("Загрузка…");
            try { await Nav.PushAsync(new ServicesCatalogPage()); }
            finally { await Ui.HideBusyAsync(); }
            return;
        }

        if (card.Service is not null)
            await Nav.PushAsync(new ServiceDetailPage(card.Service.Id, card.Service.Title));
    }

    private async void OnBookCta(object? sender, EventArgs e)
    {
        if (_services.Count == 0)
        {
            await Ui.InfoAsync("Услуги", "Список услуг пока пуст");
            return;
        }

        await Nav.PushAsync(new ServicesCatalogPage());
    }

    private async void OnLoyaltyBanner(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//profile");

    private sealed record ServiceCard(
        Guid Id,
        string Title,
        string Description,
        decimal PriceFrom,
        int DurationMinutes,
        ImageSource ImageSource,
        ServiceItem Source);

    private sealed class PreviewCard
    {
        public bool IsMore { get; init; }
        public bool IsService => !IsMore;
        public string Title { get; init; } = string.Empty;
        public string PriceText { get; init; } = string.Empty;
        public ImageSource? ImageSource { get; init; }
        public ServiceCard? Service { get; init; }

        public static PreviewCard More() => new() { IsMore = true, Title = "Ещё" };

        public static PreviewCard FromService(ServiceCard s) => new()
        {
            IsMore = false,
            Title = s.Title,
            PriceText = $"от {s.PriceFrom:0} ₽",
            ImageSource = s.ImageSource,
            Service = s
        };
    }
}
