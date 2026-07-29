using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class HistoryPage : ContentPage
{
    public HistoryPage()
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
            var raw = await App.Api.GetMyBookingsAsync() ?? [];
            var items = raw.Select(BookingListItem.From).ToList();

            var upcoming = items
                .Where(i => i.IsUpcoming)
                .OrderBy(i => i.StartAtUtc)
                .ToList();

            var past = items
                .Where(i => !i.IsUpcoming)
                .OrderByDescending(i => i.StartAtUtc)
                .ToList();

            UpcomingSection.IsVisible = upcoming.Count > 0;
            UpcomingList.ItemsSource = upcoming;
            PastList.ItemsSource = past;
            PastHeader.IsVisible = upcoming.Count > 0 || past.Count > 0;
            NoBookingsLabel.IsVisible = upcoming.Count == 0 && past.Count == 0;
        }
        catch (Exception ex)
        {
            await Ui.ErrorAsync(ex.Message);
        }
    }

    private async void OnRepeat(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: BookingListItem item }) return;

        try
        {
            var service = await Ui.RunBusyAsync(
                () => ResolveServiceAsync(item),
                "Открываем запись…");

            await Nav.PushAsync(new BookingPage(service ?? FallbackService(item)));
        }
        catch (Exception ex)
        {
            await Ui.ErrorAsync(ex.Message);
        }
    }

    private static async Task<ServiceItem> ResolveServiceAsync(BookingListItem item)
    {
        var services = await App.Api.GetServicesAsync() ?? [];
        return services.FirstOrDefault(s => s.Id == item.Source.ServiceId)
               ?? FallbackService(item);
    }

    private static ServiceItem FallbackService(BookingListItem item)
        => new(item.Source.ServiceId, item.ServiceTitle, string.Empty, string.Empty, 60, 0, null, null, null, null);
}
