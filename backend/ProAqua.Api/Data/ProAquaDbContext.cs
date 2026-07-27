using ProAqua.Api.Domain;
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
            db.Services.AddRange(
                new WashService
                {
                    Title = "Экспресс-мойка",
                    Description = "Кузов, диски, сушка. Быстро вернём блеск перед городом.",
                    Category = "wash",
                    DurationMinutes = 40,
                    PriceFrom = 800,
                    ImageUrl = "/uploads/service-wash.png",
                    SortOrder = 1
                },
                new WashService
                {
                    Title = "Комплексная мойка",
                    Description = "Снаружи и внутри: пылесос, пластик, стёкла, коврики.",
                    Category = "wash",
                    DurationMinutes = 90,
                    PriceFrom = 1800,
                    ImageUrl = "/uploads/service-interior.png",
                    SortOrder = 2
                },
                new WashService
                {
                    Title = "Глубокая очистка",
                    Description = "Пена, химия, удаление загрязнений с кузова и дисков.",
                    Category = "wash",
                    DurationMinutes = 120,
                    PriceFrom = 3500,
                    ImageUrl = "/uploads/service_deep_clean.png",
                    SortOrder = 3
                },
                new WashService
                {
                    Title = "Детейлинг",
                    Description = "Полировка, восстановление блеска и защита ЛКП.",
                    Category = "detailing",
                    DurationMinutes = 180,
                    PriceFrom = 8000,
                    ImageUrl = "/uploads/service_detailing.png",
                    SortOrder = 4
                },
                new WashService
                {
                    Title = "Керамика",
                    Description = "Защитное керамическое покрытие. Эффект «до / после» видно сразу.",
                    Category = "detailing",
                    DurationMinutes = 240,
                    PriceFrom = 12000,
                    ImageUrl = "/uploads/service-ceramic.png",
                    BeforeAfterImageUrl = "/uploads/detailing-before-after.png",
                    SortOrder = 5
                });
        }
        else
        {
            await EnsureServiceCatalogAsync(db);
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

        await db.SaveChangesAsync();
    }

    private static async Task EnsureServiceCatalogAsync(ProAquaDbContext db)
    {
        var catalog = new[]
        {
            ("Экспресс-мойка", "/uploads/service-wash.png"),
            ("Комплексная мойка", "/uploads/service-interior.png"),
            ("Глубокая очистка", "/uploads/service_deep_clean.png"),
            ("Детейлинг", "/uploads/service_detailing.png"),
            ("Керамика", "/uploads/service-ceramic.png"),
            ("Детейлинг / керамика", "/uploads/service-ceramic.png")
        };

        foreach (var (title, image) in catalog)
        {
            var svc = await db.Services.FirstOrDefaultAsync(s => s.Title == title);
            if (svc is not null)
                svc.ImageUrl = image;
        }

        if (!await db.Services.AnyAsync(s => s.Title == "Глубокая очистка"))
        {
            db.Services.Add(new WashService
            {
                Title = "Глубокая очистка",
                Description = "Пена, химия, удаление загрязнений с кузова и дисков.",
                Category = "wash",
                DurationMinutes = 120,
                PriceFrom = 3500,
                ImageUrl = "/uploads/service_deep_clean.png",
                SortOrder = 3
            });
        }

        if (!await db.Services.AnyAsync(s => s.Title == "Детейлинг" && s.Category == "detailing"))
        {
            db.Services.Add(new WashService
            {
                Title = "Детейлинг",
                Description = "Полировка, восстановление блеска и защита ЛКП.",
                Category = "detailing",
                DurationMinutes = 180,
                PriceFrom = 8000,
                ImageUrl = "/uploads/service_detailing.png",
                SortOrder = 4
            });
        }

        await db.SaveChangesAsync();
    }
}
