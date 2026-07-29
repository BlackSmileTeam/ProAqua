using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class BookingPage : ContentPage
{
    private readonly ServiceItem _service;
    private readonly int _vehicleType;
    private List<SlotItem> _slots = [];
    private DateTime? _selectedStartUtc;

    public BookingPage(ServiceItem service, int vehicleType = 0)
    {
        InitializeComponent();
        _service = service;
        _vehicleType = vehicleType;
        TitleLabel.Text = service.Title;
        DatePicker.Date = DateTime.Today.AddDays(1);
        DatePicker.MinimumDate = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSlotsAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        OnBack(null, EventArgs.Empty);
        return true;
    }

    private async void OnBack(object? sender, EventArgs e)
        => await Nav.PopAsync();

    private async void OnDateChanged(object? sender, DateChangedEventArgs e)
        => await LoadSlotsAsync();

    private async Task LoadSlotsAsync()
    {
        try
        {
            NoSlotsLabel.IsVisible = false;
            SlotsList.IsVisible = false;
            _selectedStartUtc = null;
            ConfirmButton.IsEnabled = false;
            SelectedSlotLabel.IsVisible = false;

            var localDate = DatePicker.Date;
            SelectedDateLabel.Text = $"Выбрано: {BookingFormat.Date(localDate)} · загрузка…";

            var slots = await App.Api.GetSlotsAsync(_service.Id, localDate) ?? [];
            _slots = slots.Where(s => s.Available).Select(s => new SlotItem { StartAtUtc = s.StartAt }).ToList();

            SelectedDateLabel.Text = $"Выбрано: {BookingFormat.Date(localDate)}";
            SlotsList.ItemsSource = _slots;
            NoSlotsLabel.IsVisible = _slots.Count == 0;
            SlotsList.IsVisible = _slots.Count > 0;
        }
        catch (Exception ex)
        {
            SelectedDateLabel.Text = $"Выбрано: {BookingFormat.Date(DatePicker.Date)}";
            await Ui.ErrorAsync(ex.Message);
        }
    }

    private void OnSlotTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not SlotItem slot)
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
            await Ui.InfoAsync("Время", "Выберите свободный слот из списка");
            return;
        }

        try
        {
            ConfirmButton.IsEnabled = false;
            var booking = await Ui.RunBusyAsync(
                () => App.Api.CreateBookingAsync(_service.Id, _selectedStartUtc.Value, _vehicleType),
                "Создаём запись…");

            if (booking is null)
            {
                ConfirmButton.IsEnabled = true;
                return;
            }

            var when = BookingFormat.FormatDateTime(booking.StartAt.ToLocalTime());
            var go = await Ui.ConfirmAsync(
                "Запись создана",
                $"{_service.Title}\n{when}\n\nСтатус: {BookingFormat.StatusRu(booking.Status)}",
                "Мои записи",
                "На главную",
                centerText: true);

            if (go)
                await Shell.Current.GoToAsync("//history");
            else
                await Nav.PopAsync();
        }
        catch (Exception ex)
        {
            ConfirmButton.IsEnabled = true;
            await Ui.ErrorAsync(ex.Message, "Не удалось записаться");
        }
    }
}
