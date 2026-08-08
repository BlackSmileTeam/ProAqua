namespace ProAqua.App.Services;

/// <summary>
/// Навигация для Shell: у Tab-страниц нет NavigationPage, поэтому PushAsync через page.Navigation падает.
/// Нужен Shell.Current.Navigation / GoToAsync.
/// На Android Push/Go во время BusyOverlay (modal) или двойной тап даёт
/// IllegalArgumentException: No view found for id …/labeled (fragment attach race).
/// </summary>
public static class Nav
{
    private static int _gate;

    public static INavigation? Current =>
        Shell.Current?.Navigation
        ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;

    public static bool CanPop => Current is { NavigationStack.Count: > 1 };

    /// <summary>Shell-маршрут (зарегистрированный в AppShell).</summary>
    public static Task GoToAsync(string route)
        => RunExclusiveAsync(async () =>
        {
            if (Shell.Current is null || string.IsNullOrWhiteSpace(route))
                return;

            // Никогда не навигируем поверх BusyOverlay — иначе Android теряет container id=labeled.
            await Ui.ForceClearBusyAsync();
            await Shell.Current.GoToAsync(route);
        });

    public static Task PushAsync(Page page)
        => RunExclusiveAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(page);

            await Ui.ForceClearBusyAsync();

            var nav = Current;
            if (nav is null)
                return;

            await nav.PushAsync(page);
        });

    public static Task PopAsync()
        => RunExclusiveAsync(async () =>
        {
            await Ui.ForceClearBusyAsync();

            var nav = Current;
            if (nav is not null && nav.NavigationStack.Count > 1)
            {
                await nav.PopAsync();
                return;
            }

            if (Shell.Current is not null)
                await Shell.Current.GoToAsync("//home");
        });

    private static async Task RunExclusiveAsync(Func<Task> action)
    {
        // Игнорируем повторный тап, пока идёт переход.
        if (Interlocked.CompareExchange(ref _gate, 1, 0) != 0)
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    await action();
                }
                catch (InvalidOperationException)
                {
                    // Страница уже исчезла / Navigation null mid-flight.
                }
                catch (ArgumentException)
                {
                    // Android fragment container race (labeled / NavigationRootManager).
                }
            });
        }
        finally
        {
            Interlocked.Exchange(ref _gate, 0);
        }
    }
}
