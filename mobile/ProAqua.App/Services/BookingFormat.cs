using System.Globalization;

namespace ProAqua.App.Services;

public static class BookingFormat
{
  private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

  public static string Date(DateTime local) => local.ToString("dd-MM-yyyy", Ru);

  public static string Time(DateTime local) => local.ToString("HH:mm", Ru);

  public static string FormatDateTime(DateTime local) => $"{Date(local)} {Time(local)}";

  public static string StatusRu(string? status) => status?.Trim() switch
  {
    "Pending" => "Ожидает",
    "Confirmed" => "Подтверждена",
    "InProgress" => "В работе",
    "Ready" => "Готова к выдаче",
    "Completed" => "Завершена",
    "Cancelled" => "Отменена",
    "NoShow" => "Неявка",
    _ => status ?? "—"
  };

  public static bool IsUpcoming(BookingItem booking)
  {
    if (booking.StartAt.ToUniversalTime() < System.DateTime.UtcNow)
      return false;
    return booking.Status is not "Cancelled" and not "Completed" and not "NoShow";
  }
}

public sealed class BookingListItem
{
  public Guid Id { get; init; }
  public string ServiceTitle { get; init; } = string.Empty;
  public DateTime StartAtUtc { get; init; }
  public DateTime StartAtLocal => StartAtUtc.ToLocalTime();
  public string DateText => BookingFormat.Date(StartAtLocal);
  public string TimeText => BookingFormat.Time(StartAtLocal);
  public string DateTimeText => BookingFormat.FormatDateTime(StartAtLocal);
  public string StatusRu { get; init; } = string.Empty;
  public bool IsUpcoming { get; init; }
  public bool IsCompleted { get; init; }
  public BookingItem Source { get; init; } = null!;

  public static BookingListItem From(BookingItem b) => new()
  {
    Id = b.Id,
    ServiceTitle = b.ServiceTitle,
    StartAtUtc = b.StartAt,
    StatusRu = BookingFormat.StatusRu(b.Status),
    IsUpcoming = BookingFormat.IsUpcoming(b),
    IsCompleted = string.Equals(b.Status?.Trim(), "Completed", StringComparison.OrdinalIgnoreCase),
    Source = b
  };
}

public sealed class SlotItem
{
  public DateTime StartAtUtc { get; init; }
  public DateTime StartAtLocal => StartAtUtc.ToLocalTime();
  public string TimeText => BookingFormat.Time(StartAtLocal);
  public bool IsSelected { get; set; }
}
