using ProAqua.App.Pages;

namespace ProAqua.App.Services;

/// <summary>Спиннер загрузки и стилизованные попапы ПроАква.</summary>
public static class Ui
{
    private static int _busyDepth;
    private static readonly object Sync = new();

    private static INavigation? Nav =>
        Shell.Current?.Navigation
        ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;

    public static async Task ForceClearBusyAsync()
    {
        lock (Sync) _busyDepth = 0;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var nav = Nav;
                if (nav is null) return;
                var guard = 0;
                while (guard++ < 10 && nav.ModalStack.Count > 0 && nav.ModalStack[^1] is BusyOverlayPage)
                    await nav.PopModalAsync(false);
            }
            catch { /* ignore */ }
        });
    }

    public static async Task ShowBusyAsync(string? message = null)
    {
        var shouldShow = false;
        lock (Sync)
        {
            _busyDepth++;
            shouldShow = _busyDepth == 1;
        }

        if (!shouldShow)
            return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Task.Yield();
                var nav = Nav;
                if (nav is null) return;

                // Уже висит — не дублируем
                if (nav.ModalStack.Count > 0 && nav.ModalStack[^1] is BusyOverlayPage)
                    return;

                await nav.PushModalAsync(new BusyOverlayPage(message ?? "Загрузка…"), false);
            }
            catch
            {
                lock (Sync) _busyDepth = Math.Max(0, _busyDepth - 1);
            }
        });
    }

    public static async Task HideBusyAsync()
    {
        var shouldHide = false;
        lock (Sync)
        {
            if (_busyDepth > 0)
                _busyDepth--;
            shouldHide = _busyDepth == 0;
        }

        if (!shouldHide)
            return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var nav = Nav;
                if (nav is null) return;
                var guard = 0;
                while (guard++ < 10 && nav.ModalStack.Count > 0 && nav.ModalStack[^1] is BusyOverlayPage)
                    await nav.PopModalAsync(false);
            }
            catch { /* ignore */ }
        });
    }

    public static async Task RunBusyAsync(Func<Task> action, string? message = null)
    {
        await ShowBusyAsync(message);
        try { await action(); }
        finally { await HideBusyAsync(); }
    }

    public static async Task<T?> RunBusyAsync<T>(Func<Task<T>> action, string? message = null)
    {
        await ShowBusyAsync(message);
        try { return await action(); }
        finally { await HideBusyAsync(); }
    }

    public static async Task ErrorAsync(string message, string title = "Ошибка")
    {
        await ForceClearBusyAsync();
        await ShowDialogAsync(title, message, "OK", null, isError: true);
    }

    public static async Task InfoAsync(string title, string message, string ok = "OK")
    {
        await ForceClearBusyAsync();
        await ShowDialogAsync(title, message, ok, null, isError: false);
    }

    public static async Task<bool> ConfirmAsync(
        string title, string message, string accept = "Да", string cancel = "Отмена", bool centerText = false)
    {
        await ForceClearBusyAsync();
        return await ShowDialogAsync(title, message, accept, cancel, isError: false, centerText: centerText);
    }

    private static async Task<bool> ShowDialogAsync(
        string title, string message, string primary, string? secondary, bool isError, bool centerText = false)
    {
        var tcs = new TaskCompletionSource<bool>();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var nav = Nav;
                if (nav is null)
                {
                    tcs.TrySetResult(true);
                    return;
                }

                while (nav.ModalStack.Count > 0 && nav.ModalStack[^1] is BusyOverlayPage)
                    await nav.PopModalAsync(false);

                var dialog = new AppDialogPage(title, message, primary, secondary, isError, centerText);
                await nav.PushModalAsync(dialog, false);
                tcs.TrySetResult(await dialog.Result);
            }
            catch
            {
                tcs.TrySetResult(false);
            }
        });
        return await tcs.Task;
    }
}
