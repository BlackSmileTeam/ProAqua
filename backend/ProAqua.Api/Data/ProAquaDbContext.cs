using ProAqua.Api.Domain;
using ProAqua.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ProAqua.Api.Data;

public class ProAquaDbContext(DbContextOptions<ProAquaDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<WashService> Services => Set<WashService>();
    public DbSet<WorkBay> WorkBays => Set<WorkBay>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<Promotion> Promotions => Set<Promotion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Phone).IsUnique();
            e.HasIndex(x => x.ReferralCode).IsUnique();
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.ReferralCode).HasMaxLength(16);
        });

        modelBuilder.Entity<WashService>(e =>
        {
            e.Property(x => x.PriceFrom).HasPrecision(10, 2);
            e.Property(x => x.PriceSedan).HasPrecision(10, 2);
            e.Property(x => x.PriceCrossover).HasPrecision(10, 2);
            e.Property(x => x.PriceSuv).HasPrecision(10, 2);
            e.Property(x => x.PriceSuvXl).HasPrecision(10, 2);
            e.Property(x => x.ImageData).HasColumnType("longblob");
            e.Property(x => x.ImageContentType).HasMaxLength(100);
            e.HasOne(x => x.Parent)
                .WithMany(x => x.Variants)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ParentId);
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.Property(x => x.FinalPrice).HasPrecision(10, 2);
            e.HasIndex(x => x.StartAt);
            e.HasOne(x => x.User)
                .WithMany(x => x.Bookings)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Master)
                .WithMany()
                .HasForeignKey(x => x.MasterUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DeviceToken>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Token }).IsUnique();
        });

        modelBuilder.Entity<PromoCode>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Promotion>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasIndex(x => x.EndsAt);
            e.Property(x => x.ImageData).HasColumnType("longblob");
            e.Property(x => x.ImageContentType).HasMaxLength(100);
        });
    }
}

public static class DbSeeder
{
    public static async Task SeedAsync(ProAquaDbContext db)
    {
        if (!await db.WorkBays.AnyAsync())
        {
            db.WorkBays.AddRange(
                new WorkBay { Name = "Бокс 1" },
                new WorkBay { Name = "Бокс 2" },
                new WorkBay { Name = "Детейлинг-пост" });
        }

        if (!await db.Services.AnyAsync())
        {
            // Пустой каталог — наполняется SQL-скриптом replace_services_from_site.sql
        }
        else
        {
            await EnsureServiceImagesAsync(db);
        }

        const string defaultPin = "1234";
        var defaultPinHash = BCrypt.Net.BCrypt.HashPassword(defaultPin);

        if (!await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            db.Users.Add(new AppUser
            {
                Phone = "+79000000001",
                Name = "Администратор",
                Role = UserRole.Admin,
                ReferralCode = "ADMIN01",
                LoyaltyLevel = 3,
                PinHash = defaultPinHash,
                MustChangePassword = false
            });
        }

        if (!await db.Users.AnyAsync(u => u.Role == UserRole.Master))
        {
            db.Users.Add(new AppUser
            {
                Phone = "+79000000002",
                Name = "Мастер Алексей",
                Role = UserRole.Master,
                ReferralCode = "MASTER1",
                LoyaltyLevel = 1,
                PinHash = defaultPinHash,
                MustChangePassword = false
            });
        }

        foreach (var staff in await db.Users.Where(u => u.Role != UserRole.Client && (u.PinHash == null || u.PinHash == "")).ToListAsync())
        {
            staff.PinHash = defaultPinHash;
            staff.MustChangePassword = false;
        }

        if (!await db.PromoCodes.AnyAsync())
        {
            db.PromoCodes.Add(new PromoCode
            {
                Code = "WELCOME10",
                PercentOff = 10,
                BonusPoints = 50,
                ValidUntil = DateTime.UtcNow.AddYears(1)
            });
        }

        if (!await db.Promotions.AnyAsync())
        {
            var now = DateTime.UtcNow;
            db.Promotions.AddRange(
                MakePromotion(
                    "Комплекс со скидкой 15%",
                    "При записи на комплексную мойку в будни — скидка 15%.",
                    now.Date,
                    now.Date.AddMonths(1).AddDays(1).AddTicks(-1),
                    "promo_complex.jpg"),
                MakePromotion(
                    "Керамика — бонусные баллы x2",
                    "За керамическое покрытие начисляем двойные баллы лояльности.",
                    now.Date,
                    now.Date.AddDays(45).AddDays(1).AddTicks(-1),
                    "promo_ceramic.jpg"));
        }
        else
        {
            await EnsurePromotionImagesAsync(db);
        }

        await db.SaveChangesAsync();
    }

    private static WashService MakeService(string title, string description, string category, int minutes, decimal price, int sort, string seedFile)
    {
        var data = ImageStorage.ReadSeedFile(seedFile);
        return new WashService
        {
            Title = title,
            Description = description,
            Category = category,
            DurationMinutes = minutes,
            PriceFrom = price,
            SortOrder = sort,
            ImageData = data,
            ImageContentType = data is null ? null : "image/jpeg",
            ImageUrl = null
        };
    }

    private static Promotion MakePromotion(string title, string description, DateTime starts, DateTime ends, string seedFile)
    {
        var data = ImageStorage.ReadSeedFile(seedFile);
        return new Promotion
        {
            Title = title,
            Description = description,
            StartsAt = starts,
            EndsAt = ends,
            IsActive = true,
            ImageData = data,
            ImageContentType = data is null ? null : "image/jpeg"
        };
    }

    private static async Task EnsureServiceImagesAsync(ProAquaDbContext db)
    {
        // Картинки подтягиваются SQL-скриптом replace_services_from_site.sql
        await Task.CompletedTask;
    }

    private static async Task EnsurePromotionImagesAsync(ProAquaDbContext db)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Комплекс со скидкой 15%"] = "promo_complex.jpg",
            ["Керамика — бонусные баллы x2"] = "promo_ceramic.jpg"
        };
        foreach (var promo in await db.Promotions.ToListAsync())
        {
            if (promo.ImageData is { Length: > 0 }) continue;
            if (!map.TryGetValue(promo.Title, out var file)) continue;
            var data = ImageStorage.ReadSeedFile(file);
            if (data is null) continue;
            promo.ImageData = data;
            promo.ImageContentType = "image/jpeg";
        }
    }

    private sealed record CatalogItem(string Title, string Description, string Category, int Minutes, decimal Price, int Sort, string SeedFile);

    /// <summary>Каталог как на официальном сайте ПроАква.</summary>
    private static CatalogItem[] WebsiteCatalog() =>
    [
        new("Комплексная мойка кузова", "Двухфазная мойка, очистка колёсных арок, сушка, обработка кузова.", "wash", 60, 1500, 10, "service_wash.jpg"),
        new("Комплексная мойка с уборкой в салоне", "Комплексная мойка кузова и влажная уборка салона, чистка ковриков.", "wash", 90, 2500, 20, "service_interior.jpg"),
        new("Дополнительные услуги мойки", "Мойка двигателя, чистка дисков, обработка пластика, нано-защита.", "wash", 45, 1500, 30, "service_deep.jpg"),

        new("Защитные покрытия для кузова", "Многослойное нанесение керамики для защиты ЛКП до 5 лет.", "exterior", 240, 12000, 40, "service_ceramic.jpg"),
        new("Антигравийная плёнка PPF", "Невидимая защита кузова от сколов, царапин и реагентов.", "exterior", 300, 47000, 50, "service_ceramic.jpg"),
        new("Полировка и отчистка кузова", "Профессиональная коррекция ЛКП с устранением царапин.", "exterior", 180, 8000, 60, "service_detailing.jpg"),

        new("Химчистка салона", "Глубокая чистка всех поверхностей салона.", "interior", 150, 1000, 70, "service_interior.jpg"),
        new("Перешив и реставрация салона", "Восстановление и защита кожаных поверхностей.", "interior", 240, 15000, 80, "service_interior.jpg"),
        new("Защита интерьера", "Нанесение защитных составов на все поверхности.", "interior", 90, 3500, 90, "service_interior.jpg"),

        new("Тонировка стёкол", "Профессиональная тонировка атермальными плёнками.", "other", 120, 8000, 100, "service_deep.jpg"),
        new("Тюнинг и дооснащение", "Установка дополнительного оборудования.", "other", 180, 5000, 110, "service_detailing.jpg"),
        new("Шумоизоляция", "Комплексная ШВИ для максимального комфорта.", "other", 300, 25000, 120, "service_deep.jpg"),

        new("Базовый курс Детейлинга", "Основы детейлинга для начинающих. 15 дней.", "education", 0, 60000, 130, "service_detailing.jpg"),
        new("Курс Основы оклейки плёнками", "Обучение оклейке защитной плёнкой. 25 дней.", "education", 0, 80000, 140, "service_ceramic.jpg"),
        new("Углубленный курс полного спектра", "Полный спектр услуг детейлинга. 2 месяца.", "education", 0, 150000, 150, "service_detailing.jpg"),

        new("PPF Basic", "Базовая защита передней части: бампер, капот, фары, стойки.", "ppf", 480, 47000, 160, "service_ceramic.jpg"),
        new("PPF Premium", "Расширенная защита: Basic + крылья, зона под ручками, зеркала.", "ppf", 720, 83000, 170, "service_ceramic.jpg"),
        new("PPF Ultimate", "Максимальная защита: полная оклейка кузова, броня лобового.", "ppf", 1440, 250000, 180, "service_ceramic.jpg")
    ];

    private static async Task EnsureWebsiteCatalogAsync(ProAquaDbContext db)
    {
        foreach (var item in WebsiteCatalog())
        {
            var existing = await db.Services.FirstOrDefaultAsync(s => s.Title == item.Title);
            if (existing is null)
            {
                db.Services.Add(MakeService(item.Title, item.Description, item.Category, item.Minutes, item.Price, item.Sort, item.SeedFile));
                continue;
            }

            existing.Description = item.Description;
            existing.Category = item.Category;
            existing.DurationMinutes = item.Minutes;
            existing.PriceFrom = item.Price;
            existing.SortOrder = item.Sort;
            existing.IsActive = true;
            if (existing.ImageData is null || existing.ImageData.Length == 0)
            {
                var data = ImageStorage.ReadSeedFile(item.SeedFile);
                if (data is not null)
                {
                    existing.ImageData = data;
                    existing.ImageContentType = "image/jpeg";
                }
            }
        }
    }
}
