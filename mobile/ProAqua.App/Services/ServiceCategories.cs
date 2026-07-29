namespace ProAqua.App.Services;

public static class ServiceCategories
{
    public const string Wash = "wash";
    public const string Exterior = "exterior";
    public const string Interior = "interior";
    public const string Other = "other";
    public const string Education = "education";
    public const string Ppf = "ppf";

    public static readonly (string Key, string Title, string Subtitle, string Icon)[] Ordered =
    [
        (Wash, "Детейлинг мойка", "Качественная мойка и уход", "icon_cat_wash.png"),
        (Exterior, "Экстерьер", "Защита и уход за кузовом", "icon_cat_exterior.png"),
        (Interior, "Интерьер", "Уход за салоном", "icon_cat_interior.png"),
        (Other, "Прочие услуги", "Дополнительные услуги", "icon_cat_other.png"),
        (Education, "Обучение", "Школа детейлинга", "icon_cat_education.png"),
        (Ppf, "Пакеты услуг бронирования PPF", "Защита кузова плёнкой", "icon_cat_ppf.png")
    ];

    public static string Title(string? category)
        => Ordered.FirstOrDefault(x => x.Key.Equals(category, StringComparison.OrdinalIgnoreCase)).Title
           ?? "Услуги";

    public static string Subtitle(string? category)
        => Ordered.FirstOrDefault(x => x.Key.Equals(category, StringComparison.OrdinalIgnoreCase)).Subtitle
           ?? string.Empty;

    public static string Icon(string? category)
        => Ordered.FirstOrDefault(x => x.Key.Equals(category, StringComparison.OrdinalIgnoreCase)).Icon
           ?? "icon_cat_other.png";

    public static int SortIndex(string? category)
    {
        for (var i = 0; i < Ordered.Length; i++)
        {
            if (Ordered[i].Key.Equals(category, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 99;
    }
}
