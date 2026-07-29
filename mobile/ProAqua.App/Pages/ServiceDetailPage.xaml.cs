using Microsoft.Maui.Controls.Shapes;
using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class ServiceDetailPage : ContentPage
{
    private readonly Guid _serviceId;
    private ServiceDetail? _detail;
    private ServiceVariantItem? _selected;
    private int _vehicleType;
    private readonly List<Border> _vehicleChips = [];
    private CancellationTokenSource? _loadCts;
    private bool _loadScheduled;

    public ServiceDetailPage(Guid serviceId, string? titleHint = null)
    {
        InitializeComponent();
        _serviceId = serviceId;
        if (!string.IsNullOrWhiteSpace(titleHint))
            TitleLabel.Text = titleHint;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loadScheduled) return;
        _loadScheduled = true;
        // Не блокируем OnAppearing модальным спиннером — запускаем загрузку на следующем кадре
        Dispatcher.DispatchAsync(async () => await LoadAsync());
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _loadCts?.Cancel();
        LoadingOverlay.IsVisible = false;
        _ = Ui.ForceClearBusyAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        OnBack(null, EventArgs.Empty);
        return true;
    }

    private async void OnBack(object? sender, EventArgs e)
    {
        _loadCts?.Cancel();
        LoadingOverlay.IsVisible = false;
        await Ui.ForceClearBusyAsync();
        await Nav.PopAsync();
    }

    private async void OnShowPurpose(object? sender, TappedEventArgs e)
    {
        if (_detail is null) return;

        if (!string.IsNullOrWhiteSpace(_detail.DetailsHtml))
        {
            DetailsCard.IsVisible = !DetailsCard.IsVisible;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_detail.Purpose))
            await Ui.InfoAsync("Для чего услуга", _detail.Purpose!);
    }

    private async Task LoadAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        LoadingOverlay.IsVisible = true;
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ServiceDetail] GET service {_serviceId} base={ProAquaApi.BaseUrl}");
            _detail = await App.Api.GetServiceDetailAsync(_serviceId);
            if (ct.IsCancellationRequested)
                return;

            if (_detail is null)
            {
                BookButton.IsVisible = false;
                await Ui.ErrorAsync("Услуга не найдена");
                await Nav.PopAsync();
                return;
            }

            TitleLabel.Text = _detail.Title;
            DescriptionLabel.Text = _detail.Description;
            if (!string.IsNullOrWhiteSpace(_detail.ImageUrl))
                HeroImage.Source = ImageSource.FromUri(new Uri(_detail.ImageUrl));

            PurposeBadge.IsVisible =
                !string.IsNullOrWhiteSpace(_detail.Purpose) || !string.IsNullOrWhiteSpace(_detail.DetailsHtml);

            DetailsCard.IsVisible = false;
            if (!string.IsNullOrWhiteSpace(_detail.DetailsHtml))
            {
                DetailsWeb.Source = new HtmlWebViewSource
                {
                    Html = WrapDetailsHtml(_detail.DetailsHtml!)
                };
            }
            else
            {
                DetailsWeb.Source = null;
            }

            BuildVehicleChips();
            BuildVariants();
            UpdateBookButtonVisibility();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ServiceDetail] load error: {ex}");
            if (!ct.IsCancellationRequested)
                await Ui.ErrorAsync(ex.Message);
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
        }
    }

    private static string WrapDetailsHtml(string innerHtml)
        => """
           <html>
             <head>
               <meta name="viewport" content="width=device-width, initial-scale=1.0" />
               <style>
                 body { margin: 0; padding: 8px; font-family: -apple-system, Segoe UI, Arial, sans-serif; color: #E8F4F6; background: transparent; }
                 h1,h2,h3,h4 { color: #FFFFFF; margin: 14px 0 8px; }
                 p, li { line-height: 1.45; color: #CFE3E8; }
                 table { width: 100%; border-collapse: collapse; margin: 10px 0; font-size: 14px; }
                 th, td { border: 1px solid #2B4A54; padding: 8px; vertical-align: top; }
                 th { background: #123647; color: #FFFFFF; text-align: left; }
                 tr:nth-child(even) td { background: #0F2A36; }
                 .badge { display:inline-block; padding:4px 8px; border-radius:10px; background:#163A47; color:#26C6DA; font-weight:600; }
               </style>
             </head>
             <body>
           """
        + innerHtml +
        """
             </body>
           </html>
           """;

    private void BuildVehicleChips()
    {
        VehicleTypesHost.Children.Clear();
        _vehicleChips.Clear();
        var labels = new[] { ("Седан", 0), ("Кроссовер", 1), ("Внедорожник", 2), ("Внедорожник XL", 3) };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            RowSpacing = 8,
            HorizontalOptions = LayoutOptions.Fill
        };

        for (var i = 0; i < labels.Length; i++)
        {
            var (text, type) = labels[i];
            var chip = MakeChip(text, type == _vehicleType, fillWidth: true);
            var captured = type;
            chip.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    _vehicleType = captured;
                    RefreshVehicleChips();
                    RefreshVariantPrices();
                    UpdateSelectedPrice();
                })
            });
            _vehicleChips.Add(chip);
            grid.Add(chip, i % 2, i / 2);
        }

        VehicleTypesHost.Children.Add(grid);
    }

    private void RefreshVehicleChips()
    {
        var labels = new[] { "Седан", "Кроссовер", "Внедорожник", "Внедорожник XL" };
        for (var i = 0; i < _vehicleChips.Count; i++)
        {
            var selected = i == _vehicleType;
            _vehicleChips[i].Stroke = selected
                ? (Brush)Application.Current!.Resources["PrimaryBrush"]
                : Color.FromArgb("#2B4A54");
            _vehicleChips[i].BackgroundColor = selected ? Color.FromArgb("#2626C6DA") : Colors.Transparent;
            if (_vehicleChips[i].Content is Label lbl)
                lbl.TextColor = selected ? Color.FromArgb("#26C6DA") : Color.FromArgb("#8FB3BC");
            if (i < labels.Length && _vehicleChips[i].Content is Label l)
                l.Text = labels[i];
        }
    }

    private static Border MakeChip(string text, bool selected, bool fillWidth = false) => new()
    {
        StrokeThickness = 1,
        Stroke = selected ? (Brush)Application.Current!.Resources["PrimaryBrush"] : Color.FromArgb("#2B4A54"),
        StrokeShape = new RoundRectangle { CornerRadius = 14 },
        BackgroundColor = selected ? Color.FromArgb("#2626C6DA") : Colors.Transparent,
        Padding = new Thickness(12, 10),
        HorizontalOptions = fillWidth ? LayoutOptions.Fill : LayoutOptions.Start,
        Content = new Label
        {
            Text = text,
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = selected ? Color.FromArgb("#26C6DA") : Color.FromArgb("#8FB3BC")
        }
    };

    private void BuildVariants()
    {
        VariantsHost.Children.Clear();
        _selected = null;
        SelectedPriceLabel.IsVisible = false;
        BookButton.IsEnabled = false;

        if (_detail?.Variants is null || _detail.Variants.Count == 0)
        {
            VariantsHost.Children.Add(new Label
            {
                Text = "Для этой услуги пока нет вариантов",
                TextColor = Color.FromArgb("#8FB3BC")
            });
            return;
        }

        foreach (var v in _detail.Variants)
        {
            var card = new Border
            {
                StrokeThickness = 1,
                Stroke = (Brush)Application.Current!.Resources["CardStrokeBrush"],
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                BackgroundColor = Color.FromArgb("#140A2F3F"),
                Padding = 12
            };
            var price = PriceFor(v, _vehicleType);
            var title = new Label { Text = v.Title, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, FontSize = 14 };
            var desc = new Label
            {
                Text = string.IsNullOrWhiteSpace(v.Description) ? " " : v.Description,
                TextColor = Color.FromArgb("#8FB3BC"),
                FontSize = 12,
                IsVisible = !string.IsNullOrWhiteSpace(v.Description)
            };
            var priceLbl = new Label
            {
                Text = $"{price:0} ₽",
                TextColor = Color.FromArgb("#26C6DA"),
                FontAttributes = FontAttributes.Bold,
                FontSize = 15
            };
            card.Content = new VerticalStackLayout { Spacing = 4, Children = { title, desc, priceLbl } };
            card.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    _selected = v;
                    HighlightSelected();
                    UpdateSelectedPrice();
                    if (!string.IsNullOrWhiteSpace(v.ImageUrl))
                        HeroImage.Source = ImageSource.FromUri(new Uri(v.ImageUrl!));
                })
            });
            card.ClassId = v.Id.ToString();
            VariantsHost.Children.Add(card);
        }
    }

    private void RefreshVariantPrices()
    {
        if (_detail is null) return;
        foreach (var child in VariantsHost.Children.OfType<Border>())
        {
            if (!Guid.TryParse(child.ClassId, out var id)) continue;
            var v = _detail.Variants.FirstOrDefault(x => x.Id == id);
            if (v is null || child.Content is not VerticalStackLayout stack) continue;
            if (stack.Children.OfType<Label>().LastOrDefault() is { } priceLbl)
                priceLbl.Text = $"{PriceFor(v, _vehicleType):0} ₽";
        }
    }

    private void HighlightSelected()
    {
        foreach (var child in VariantsHost.Children.OfType<Border>())
        {
            var selected = _selected is not null && child.ClassId == _selected.Id.ToString();
            child.Stroke = selected
                ? (Brush)Application.Current!.Resources["PrimaryBrush"]
                : (Brush)Application.Current!.Resources["CardStrokeBrush"];
            child.BackgroundColor = selected ? Color.FromArgb("#2626C6DA") : Color.FromArgb("#140A2F3F");
        }
        BookButton.IsEnabled = _selected is not null;
    }

    private void UpdateSelectedPrice()
    {
        if (_selected is null)
        {
            SelectedPriceLabel.IsVisible = false;
            BookButton.IsEnabled = false;
            return;
        }

        SelectedPriceLabel.IsVisible = true;
        SelectedPriceLabel.Text = $"Итого: {PriceFor(_selected, _vehicleType):0} ₽";
        BookButton.IsEnabled = true;
    }

    private void UpdateBookButtonVisibility()
    {
        var hasVariants = _detail?.Variants is { Count: > 0 };
        BookButton.IsVisible = hasVariants;
        if (!hasVariants)
            BookButton.IsEnabled = false;
    }

    private static decimal PriceFor(ServiceVariantItem v, int type) => type switch
    {
        1 => v.PriceCrossover,
        2 => v.PriceSuv,
        3 => v.PriceSuvXl,
        _ => v.PriceSedan
    };

    private async void OnBook(object? sender, EventArgs e)
    {
        if (_selected is null || _detail is null) return;
        var item = new ServiceItem(
            _selected.Id,
            _selected.Title,
            _selected.Description,
            _detail.Category,
            _selected.DurationMinutes,
            PriceFor(_selected, _vehicleType),
            _selected.ImageUrl,
            null,
            _detail.Purpose,
            null,
            !string.IsNullOrWhiteSpace(_selected.ImageUrl));
        await Nav.PushAsync(new BookingPage(item, _vehicleType));
    }
}
