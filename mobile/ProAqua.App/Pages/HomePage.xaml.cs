using ProAqua.App.Services;



namespace ProAqua.App.Pages;



public partial class HomePage : ContentPage

{

    private List<ServiceCard> _services = [];



    public HomePage()

    {

        InitializeComponent();

        PromoCarousel.ItemsSource = new List<PromoSlide>

        {

            new("hero_wash.png", "Премиальный уход для вашего автомобиля", "Чистота. Блеск. Защита."),

            new("service_deep_clean.png", "Глубокая очистка кузова", "Пена, химия, идеальный результат"),

            new("service_detailing.png", "Детейлинг и полировка", "Восстановление блеска ЛКП"),

            new("service_ceramic.png", "Керамическое покрытие", "Долгая защита кузова"),

            new("service_interior.png", "Комплексная мойка салона", "Свежесть и порядок внутри")

        };

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

            ServicesList.ItemsSource = _services;

            ServicesList.Margin = new Thickness(20, 0, 8, 0);

            await LoadUpcomingBookingAsync();

        }

        catch (Exception ex)

        {

            await DisplayAlert("Ошибка", ex.Message, "OK");

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

        => await Shell.Current.GoToAsync("//history");



    private static ServiceCard MapCard(ServiceItem s)

    {

        var title = s.Title.ToLowerInvariant();

        var (image, icon) = title switch

        {

            var t when t.Contains("глубок") => ("service_deep_clean.png", "icon_service_deep.png"),

            var t when t.Contains("керами") => ("service_ceramic.png", "icon_service_ceramic.png"),

            var t when t.Contains("детейл") => ("service_detailing.png", "icon_service_ceramic.png"),

            var t when t.Contains("комплекс") || t.Contains("салон") || t.Contains("внутр") => ("service_interior.png", "icon_service_interior.png"),

            _ => ("service_wash.png", "icon_service_wash.png")

        };



        return new ServiceCard(s.Id, s.Title, s.Description, s.PriceFrom, s.DurationMinutes, image, icon, s);

    }



    private async void OnServiceTapped(object? sender, TappedEventArgs e)

    {

        if (e.Parameter is ServiceCard card)

            await Navigation.PushAsync(new BookingPage(card.Source));

    }



    private async void OnBookCta(object? sender, EventArgs e)

    {

        if (_services.Count == 0)

        {

            await DisplayAlert("Услуги", "Список услуг пока пуст", "OK");

            return;

        }



        if (_services.Count == 1)

        {

            await Navigation.PushAsync(new BookingPage(_services[0].Source));

            return;

        }



        var titles = _services.Select(s => s.Title).ToArray();

        var pick = await DisplayActionSheet("Выберите услугу", "Отмена", null, titles);

        if (pick is null || pick == "Отмена") return;

        var selected = _services.FirstOrDefault(s => s.Title == pick);

        if (selected is not null)

            await Navigation.PushAsync(new BookingPage(selected.Source));

    }



    private async void OnLoyaltyBanner(object? sender, TappedEventArgs e)

    {

        await Shell.Current.GoToAsync("//profile");

    }



    private sealed record PromoSlide(string Image, string Title, string Subtitle);



    private sealed record ServiceCard(

        Guid Id,

        string Title,

        string Description,

        decimal PriceFrom,

        int DurationMinutes,

        string ImageUrl,

        string IconImage,

        ServiceItem Source);

}


