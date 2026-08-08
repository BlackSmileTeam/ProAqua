using Microsoft.Maui.Controls.Shapes;
using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class HomePage : ContentPage
{
    private const double PromoSideInset = 16;
    private const double PromoSlideSpacing = 12;
    private List<ServiceCard> _services = [];
    private double _pageWidth;
    private double _promoSlideWidth;
    private int _promoIndex;
    private bool _promoSnapping;
    private CancellationTokenSource? _promoSnapCts;

    public HomePage()
    {
        InitializeComponent();
    }

    private double PromoStride => _promoSlideWidth + PromoSlideSpacing;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || Math.Abs(width - _pageWidth) < 0.5)
            return;

        _pageWidth = width;
        _promoSlideWidth = Math.Max(0, width - PromoSideInset * 2);
        ApplyPromoSlideWidths();
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
            LoadPromoSlides();

            await LoadUpcomingBookingAsync();
        }
        catch (Exception ex)
        {
            var msg = string.IsNullOrWhiteSpace(ex.Message) ? "Нет связи" : ex.Message;
            if (msg.Contains("HttpRequest", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("connection", StringComparison.OrdinalIgnoreCase))
                msg = "Нет связи";
            await Ui.ErrorAsync(msg);
        }
    }

    private void LoadPromoSlides()
    {
        var slideWidth = _promoSlideWidth > 0
            ? _promoSlideWidth
            : Math.Max(0, (Width > 0 ? Width : 360) - PromoSideInset * 2);

        var cards = new List<PromoPreviewCard>
        {
            PromoPreviewCard.Slide("service_wash.png", "Премиальный уход для вашего автомобиля", "Чистота. Блеск. Защита.", slideWidth),
            PromoPreviewCard.Slide("service_deep_clean.png", "Глубокая очистка кузова", "Пена, химия, идеальный результат", slideWidth),
            PromoPreviewCard.Slide("service_detailing.png", "Детейлинг и полировка", "Восстановление блеска ЛКП", slideWidth),
            PromoPreviewCard.Slide("service_ceramic.png", "Керамическое покрытие", "Долгая защита кузова", slideWidth),
            PromoPreviewCard.Slide("service_interior.png", "Комплексная мойка салона", "Свежесть и порядок внутри", slideWidth),
        };

        _promoSnapCts?.Cancel();
        _promoSnapping = false;
        BindableLayout.SetItemsSource(PromoRow, cards);
        ApplyPromoSlideWidths();
        RebuildPromoDashIndicators(cards.Count);
        _promoIndex = 0;
        UpdatePromoDashIndicators(0);
        _ = PromoScroll.ScrollToAsync(0, 0, animated: false);
    }

    private void ApplyPromoSlideWidths()
    {
        if (_promoSlideWidth <= 0)
            return;

        if (BindableLayout.GetItemsSource(PromoRow) is IEnumerable<PromoPreviewCard> cards)
        {
            foreach (var card in cards)
                card.SlideWidth = _promoSlideWidth;
        }

        foreach (var child in PromoRow.Children)
        {
            if (child is Border border)
                border.WidthRequest = _promoSlideWidth;
        }
    }

    private void RebuildPromoDashIndicators(int count)
    {
        PromoDashIndicators.Children.Clear();
        for (var i = 0; i < count; i++)
            PromoDashIndicators.Children.Add(CreatePromoDash(i == 0));
    }

    private static Border CreatePromoDash(bool active)
    {
        // Border + RoundRectangle renders pill ends reliably on Android devices;
        // BoxView CornerRadius often draws square with odd stroke artifacts.
        return new Border
        {
            HeightRequest = 4,
            WidthRequest = active ? 22 : 12,
            StrokeThickness = 0,
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 2 },
            BackgroundColor = active
                ? (Color)Application.Current!.Resources["Primary"]
                : (Color)Application.Current!.Resources["Gray600"],
            VerticalOptions = LayoutOptions.Center,
            Padding = 0
        };
    }

    private void OnPromoScrolled(object? sender, ScrolledEventArgs e)
    {
        if (_promoSlideWidth <= 0 || _promoSnapping)
            return;

        var stride = PromoStride;
        if (stride <= 0)
            return;

        var count = PromoDashIndicators.Children.Count;
        if (count == 0)
            return;

        var index = Math.Clamp((int)Math.Round(e.ScrollX / stride), 0, count - 1);
        if (index != _promoIndex)
            UpdatePromoDashIndicators(index);

        // Debounce: when inertia/scroll settles, snap to nearest full slide
        _promoSnapCts?.Cancel();
        _promoSnapCts = new CancellationTokenSource();
        var token = _promoSnapCts.Token;
        _ = SnapPromoWhenSettledAsync(token);
    }

    private async Task SnapPromoWhenSettledAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(140, token);
            await SnapPromoToNearestAsync();
        }
        catch (TaskCanceledException)
        {
            // Still scrolling — wait for next settle
        }
    }

    private async Task SnapPromoToNearestAsync()
    {
        if (_promoSlideWidth <= 0 || _promoSnapping)
            return;

        var stride = PromoStride;
        if (stride <= 0)
            return;

        var count = PromoDashIndicators.Children.Count;
        if (count == 0)
            return;

        var index = Math.Clamp((int)Math.Round(PromoScroll.ScrollX / stride), 0, count - 1);
        var targetX = index * stride;
        if (Math.Abs(PromoScroll.ScrollX - targetX) < 1.5)
        {
            UpdatePromoDashIndicators(index);
            return;
        }

        _promoSnapping = true;
        try
        {
            await PromoScroll.ScrollToAsync(targetX, 0, animated: true);
            UpdatePromoDashIndicators(index);
        }
        finally
        {
            _promoSnapping = false;
        }
    }

    private void UpdatePromoDashIndicators(int activeIndex)
    {
        _promoIndex = activeIndex;
        for (var i = 0; i < PromoDashIndicators.Children.Count; i++)
        {
            if (PromoDashIndicators.Children[i] is not Border dash)
                continue;

            var isActive = i == activeIndex;
            dash.WidthRequest = isActive ? 22 : 12;
            dash.BackgroundColor = isActive
                ? (Color)Application.Current!.Resources["Primary"]
                : (Color)Application.Current!.Resources["Gray600"];
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

    private async void OnOpenBookings(object? sender, TappedEventArgs e)
    {
        try { await Shell.Current.GoToAsync("//history"); }
        catch { /* navigation race */ }
    }

    private async void OnOpenServicesCatalog(object? sender, TappedEventArgs e)
        => await Nav.GoToAsync(nameof(ServicesCatalogPage));

    private static ServiceCard MapCard(ServiceItem s)
    {
        var image = App.Api.ResolveMediaSource(s.ImageUrl, FallbackLocalFile(s.Title));
        return new ServiceCard(s.Id, s.Title, s.Description, s.PriceFrom, s.DurationMinutes, image, s);
    }

    private static string FallbackLocalFile(string title)
    {
        var t = title.ToLowerInvariant();
        return t switch
        {
            var x when x.Contains("глубок") || x.Contains("полир") => "service_deep_clean.png",
            var x when x.Contains("керами") || x.Contains("ppf") || x.Contains("плён") || x.Contains("плен") => "service_ceramic.png",
            var x when x.Contains("детейл") => "service_detailing.png",
            var x when x.Contains("химчист") || x.Contains("салон") || x.Contains("интерьер") || x.Contains("комплекс") => "service_interior.png",
            _ => "service_wash.png"
        };
    }

    private async void OnPreviewCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not PreviewCard card)
            return;

        // Не открываем каталог/деталь поверх BusyOverlay — на Android это роняет
        // fragment attach к id/labeled. Каталог сам грузит данные в OnAppearing.
        if (card.IsMore)
        {
            await Nav.GoToAsync(nameof(ServicesCatalogPage));
            return;
        }

        if (card.Service is not null)
            await Nav.GoToAsync(ServiceDetailPage.Route(card.Service.Id, card.Service.Title));
    }

    private async void OnBookCta(object? sender, EventArgs e)
    {
        if (_services.Count == 0)
        {
            await Ui.InfoAsync("Услуги", "Список услуг пока пуст");
            return;
        }

        await Nav.GoToAsync(nameof(ServicesCatalogPage));
    }

    private async void OnLoyaltyBanner(object? sender, TappedEventArgs e)
    {
        try { await Shell.Current.GoToAsync("//profile"); }
        catch { /* navigation race */ }
    }

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

    private void OnPromoCardTapped(object? sender, TappedEventArgs e)
    {
        // Marketing slides — tap is intentionally a no-op.
    }

    private sealed class PromoPreviewCard
    {
        public bool IsMore { get; init; }
        public bool IsPromotion => !IsMore;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public ImageSource? ImageSource { get; init; }
        public double SlideWidth { get; set; } = 360;

        public static PromoPreviewCard Slide(string imageFile, string title, string description, double slideWidth) => new()
        {
            IsMore = false,
            Title = title,
            Description = description,
            ImageSource = ImageSource.FromFile(imageFile),
            SlideWidth = slideWidth
        };
    }
}
