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
                    TextColor = Color.FromArgb("#CCCCCC"),
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

        // Services sit inside the same outer border as the header (no nested card strokes).
        var body = new VerticalStackLayout { Spacing = 0, IsVisible = expanded };
        for (var i = 0; i < group.Count; i++)
        {
            if (i > 0)
            {
                body.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    Color = Color.FromArgb("#33D4AF37"),
                    Margin = new Thickness(0, 8, 0, 8)
                });
            }

            body.Children.Add(BuildServiceRow(group[i]));
        }

        var chevron = new Label
        {
            Text = expanded ? "▾" : "▸",
            FontSize = 18,
            TextColor = Color.FromArgb("#D4AF37"),
            VerticalOptions = LayoutOptions.Center
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
                new Label { Text = subtitle, FontSize = 12, TextColor = Color.FromArgb("#CCCCCC") }
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

        var headerTap = new ContentView { Content = headerGrid };
        headerTap.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                _expanded[key] = !_expanded.GetValueOrDefault(key, false);
                body.IsVisible = _expanded[key];
                chevron.Text = _expanded[key] ? "▾" : "▸";
            })
        });

        var content = new VerticalStackLayout
        {
            Spacing = 10,
            Children = { headerTap, body }
        };

        // One shared outline for title (+ list when expanded).
        return new Border
        {
            StrokeThickness = 1,
            Stroke = (Brush)Application.Current!.Resources["CardStrokeBrush"],
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            BackgroundColor = Color.FromArgb("#140A0A0A"),
            Padding = 14,
            Content = content
        };
    }

    private View BuildServiceRow(ServiceItem service)
    {
        var image = App.Api.ResolveMediaSource(service.ImageUrl, "service_wash.png");

        // No stroke — parent category Border already outlines the whole group.
        var card = new Border
        {
            StrokeThickness = 0,
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Color.FromArgb("#1A0A0A0A"),
            Padding = 0
        };

        var grid = new Grid
        {
            IsClippedToBounds = true,
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
                    TextColor = Color.FromArgb("#CCCCCC"),
                    MaxLines = 2,
                    LineBreakMode = LineBreakMode.TailTruncation
                },
                new Label
                {
                    Text = $"от {service.PriceFrom:0} ₽",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#D4AF37")
                }
            }
        }, 1);

        card.Content = grid;
        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                try
                {
                    await Nav.GoToAsync(ServiceDetailPage.Route(service.Id, service.Title));
                }
                catch
                {
                    // Ignore navigation races (double-tap / disappearing page).
                }
            })
        });
        return card;
    }
}
