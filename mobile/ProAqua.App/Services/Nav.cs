namespace ProAqua.App.Services;

/// <summary>
/// Навигация для Shell: у Tab-страниц нет NavigationPage, поэтому PushAsync через page.Navigation падает.
/// Нужен Shell.Current.Navigation.
/// </summary>
public static class Nav
{
    public static INavigation? Current =>
        Shell.Current?.Navigation
        ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;

    public static Task PushAsync(Page page)
    {
        var nav = Current;
        if (nav is null)
            throw new InvalidOperationException("Нет активной навигации (Shell не загружен).");
        return nav.PushAsync(page);
    }

    public static Task PopAsync()
    {
        var nav = Current;
        if (nav is not null && nav.NavigationStack.Count > 1)
            return nav.PopAsync();

        if (Shell.Current is not null)
            return Shell.Current.GoToAsync("//home");

        return Task.CompletedTask;
    }

    public static bool CanPop => Current is { NavigationStack.Count: > 1 };
}
