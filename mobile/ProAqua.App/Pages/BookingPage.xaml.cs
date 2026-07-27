using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class BookingPage : ContentPage
{
    private readonly ServiceItem _service;
    private List<SlotItem> _slots = [];
    private DateTime? _selectedStartUtc;

    public BookingPage(ServiceItem service)
    {
        InitializeComponent();
        _service = service;
        TitleLabel.Text = service.Title;
        DatePicker.Date = DateTime.Today.AddDays(1);
        DatePicker.MinimumDate = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSlotsAsync();
    }

    private async void OnDateChanged(object? sender, DateChangedEventArgs e)
        => await LoadSlotsAsync();

    private async Task LoadSlotsAsync()
    {
        try
        {
            SlotsLoading.IsRunning = true;
            SlotsLoading.IsVisible = true;
            NoSlotsLabel.IsVisible = false;
            SlotsList.IsVisible = true;
            _selectedStartUtc = null;
            ConfirmButton.IsEnabled = false;
            SelectedSlotLabel.IsVisible = false;

            var localDate = DatePicker.Date;
            SelectedDateLabel.Text = $"Выбрано: {BookingFormat.Date(localDate)}";

            var slots = await App.Api.GetSlotsAsync(_service.Id, localDate) ?? [];
            _slots = slots.Where(s => s.Available).Select(s => new SlotItem { StartAtUtc = s.StartAt }).ToList();

            SlotsList.ItemsSource = _slots;
            NoSlotsLabel.IsVisible = _slots.Count == 0;
            SlotsList.IsVisible = _slots.Count > 0;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", ex.Message, "OK");
        }
        finally
        {
            SlotsLoading.IsRunning = false;
            SlotsLoading.IsVisible = false;
        }
    }

    private void OnSlotSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SlotItem slot)
            return;

        _selectedStartUtc = slot.StartAtUtc;
        ConfirmButton.IsEnabled = true;
        SelectedSlotLabel.IsVisible = true;
        SelectedSlotLabel.Text = $"Запись: {BookingFormat.FormatDateTime(slot.StartAtLocal)}";
    }

    private async void OnConfirm(object? sender, EventArgs e)
    {
        if (_selectedStartUtc is null)
        {
            await DisplayAlert("Время", "Выберите свободный слот из списка", "OK");
            return;
        }

        try
        {
            ConfirmButton.IsEnabled = false;
            var booking = await App.Api.CreateBookingAsync(_service.Id, _selectedStartUtc.Value);
            var when = BookingFormat.FormatDateTime(booking.StartAt.ToLocalTime());

            var go = await DisplayAlert(
                "Запись создана",
                $"{_service.Title}\n{when}\n\nСтатус: {BookingFormat.StatusRu(booking.Status)}",
                "Мои записи",
                "На главную");

            if (go)
                await Shell.Current.GoToAsync("//history");
            else
                await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            ConfirmButton.IsEnabled = true;
            await DisplayAlert("Не удалось записаться", ex.Message, "OK");
        }
    }
}
