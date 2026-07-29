using Microsoft.Maui.Controls.Shapes;
using ProAqua.App.Services;

namespace ProAqua.App.Pages;

public partial class ServicesCatalogPage : ContentPage
{
    private readonly Dictionary<string, bool> _expanded = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLoading;

    public ServicesCatalogPage()
    {
        InitializeComponent();
        foreach (var (key, _, _, _) in ServiceCategories.Ordered)
            _expanded[key] = false;
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
        if (_isLoading) return;
        _isLoading = true;
        try
        {
            var items = await App.Api.GetServicesAsync() ?? [];
            CategoriesHost.Children.Clear();

            foreach (var (key, title, subtitle, icon) in ServiceCategories.Ordered)
            {
                var group = items
                    .Where(s => string.Equals(s.Category, key, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s.Title)
                    .ToList();
                if (group.Count == 0)
                    continue;

                CategoriesHost.Children.Add(BuildCategorySection(key, title, subtitle, icon, group));
            }

            if (CategoriesHost.Children.Count == 0)
            {
                CategoriesHost.Children.Add(new Label
                {
                    Text = "Список услуг пока пуст",
                    TextColor = Color.FromArgb("#8FB3BC"),
                    FontSize = 14
                });
            }
        }
        catch (Exception ex)
        {
            await Ui.ErrorAsync(ex.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private View BuildCategorySection(string key, string title, string subtitle, string icon, List<ServiceItem> group)
    {
        var expanded = _expanded.GetValueOrDefault(key, false);
        var body = new VerticalStackLayout { Spacing = 10, IsVisible = expanded };
        foreach (var service in group)
            body.Children.Add(BuildServiceRow(service));

        var chevron = new Label
        {
            Text = expanded ? "▾" : "▸",
            FontSize = 18,
            TextColor = Color.FromArgb("#26C6DA"),
            VerticalOptions = LayoutOptions.Center
        };

        var header = new Border
        {
            StrokeThickness = 1,
            Stroke = (Brush)Application.Current!.Resources["CardStrokeBrush"],
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            BackgroundColor = Color.FromArgb("#140A2F3F"),
            Padding = 14
        };
        var categoryIcon = new Image
        {
            Source = ImageSource.FromFile(icon),
            WidthRequest = 44,
            HeightRequest = 44,
            Aspect = Aspect.AspectFit,
            VerticalOptions = LayoutOptions.Center,
            BackgroundColor = Colors.Transparent
        };

        var titles = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                new Label { Text = subtitle, FontSize = 12, TextColor = Color.FromArgb("#8FB3BC") }
            }
        };

        var headerGrid = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Star),
                new(GridLength.Auto)
            }
        };
        headerGrid.Add(categoryIcon, 0);
        headerGrid.Add(titles, 1);
        headerGrid.Add(chevron, 2);
        header.Content = headerGrid;

        header.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                _expanded[key] = !_expanded.GetValueOrDefault(key, false);
                body.IsVisible = _expanded[key];
                chevron.Text = _expanded[key] ? "▾" : "▸";
            })
        });

        return new VerticalStackLayout
        {
            Spacing = 10,
            Children = { header, body }
        };
    }

    private View BuildServiceRow(ServiceItem service)
    {
        ImageSource image = !string.IsNullOrWhiteSpace(service.ImageUrl)
            ? ImageSource.FromUri(new Uri(service.ImageUrl))
            : ImageSource.FromFile("service_wash.png");

        var card = new Border
        {
            StrokeThickness = 1,
            Stroke = (Brush)Application.Current!.Resources["CardStrokeBrush"],
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            BackgroundColor = Color.FromArgb("#140A2F3F"),
            Padding = 0
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(new GridLength(96)),
                new(GridLength.Star)
            }
        };

        grid.Add(new Image
        {
            Source = image,
            Aspect = Aspect.AspectFill,
            HeightRequest = 88,
            WidthRequest = 96
        }, 0);

        grid.Add(new VerticalStackLayout
        {
            Padding = new Thickness(12, 10),
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = service.Title,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                new Label
                {
                    Text = service.Description,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#8FB3BC"),
                    MaxLines = 2,
                    LineBreakMode = LineBreakMode.TailTruncation
                },
                new Label
                {
                    Text = $"от {service.PriceFrom:0} ₽",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#26C6DA")
                }
            }
        }, 1);

        card.Content = grid;
        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                await Nav.PushAsync(new ServiceDetailPage(service.Id, service.Title));
            })
        });
        return card;
    }
}
