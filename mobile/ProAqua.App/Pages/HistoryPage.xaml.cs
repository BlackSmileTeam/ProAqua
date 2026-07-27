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
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    private async void OnRepeat(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: BookingListItem item }) return;
        await Navigation.PushAsync(new BookingPage(
            new ServiceItem(item.Source.ServiceId, item.ServiceTitle, string.Empty, string.Empty, 60, 0, null, null)));
    }
}
