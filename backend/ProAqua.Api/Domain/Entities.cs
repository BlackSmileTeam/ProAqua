namespace ProAqua.Api.Domain;

public enum UserRole
{
    Client = 0,
    Master = 1,
    Admin = 2
}

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    InProgress = 2,
    Ready = 3,
    Completed = 4,
    Cancelled = 5,
    NoShow = 6
}

public enum VehicleType
{
    Sedan = 0,
    Crossover = 1,
    Suv = 2,
    /// <summary>Внедорожник XL (ранее Van).</summary>
    SuvXl = 3
}

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Phone { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    /// <summary>Хеш пароля (BCrypt).</summary>
    public string PinHash { get; set; } = string.Empty;
    /// <summary>true — нужно сменить пароль при следующем входе.</summary>
    public bool MustChangePassword { get; set; } = true;
    public UserRole Role { get; set; } = UserRole.Client;
    public string ReferralCode { get; set; } = string.Empty;
    public Guid? ReferredByUserId { get; set; }
    public int LoyaltyPoints { get; set; }
    public int LoyaltyLevel { get; set; } = 1;
    public long? AmoContactId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<DeviceToken> Devices { get; set; } = new List<DeviceToken>();
}

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? PlateNumber { get; set; }
    public VehicleType Type { get; set; } = VehicleType.Sedan;
}

public class WashService
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "wash";
    public int DurationMinutes { get; set; }
    public decimal PriceFrom { get; set; }
    public string? ImageUrl { get; set; }
    public string? BeforeAfterImageUrl { get; set; }
    /// <summary>Короткое пояснение "для чего" нужна услуга.</summary>
    public string? Purpose { get; set; }
    /// <summary>Расширенное описание/детали (можно HTML/таблицы).</summary>
    public string? DetailsHtml { get; set; }
    /// <summary>Бинарные данные картинки услуги (хранится в БД).</summary>
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; } = "image/jpeg";
    /// <summary>null = группа в каталоге; иначе Id родительской услуги.</summary>
    public Guid? ParentId { get; set; }
    public WashService? Parent { get; set; }
    public List<WashService> Variants { get; set; } = [];
    public decimal? PriceSedan { get; set; }
    public decimal? PriceCrossover { get; set; }
    public decimal? PriceSuv { get; set; }
    public decimal? PriceSuvXl { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public decimal PriceFor(VehicleType type) => type switch
    {
        VehicleType.Crossover => PriceCrossover ?? PriceFrom,
        VehicleType.Suv => PriceSuv ?? PriceFrom,
        VehicleType.SuvXl => PriceSuvXl ?? PriceFrom,
        _ => PriceSedan ?? PriceFrom
    };
}

public class WorkBay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public Guid ServiceId { get; set; }
    public WashService? Service { get; set; }
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Guid? WorkBayId { get; set; }
    public WorkBay? WorkBay { get; set; }
    public Guid? MasterUserId { get; set; }
    public AppUser? Master { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public string? Comment { get; set; }
    public decimal? FinalPrice { get; set; }
    public long? AmoLeadId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class DeviceToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class LoyaltyTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public int PointsDelta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? BookingId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PromoCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public int PercentOff { get; set; }
    public int? BonusPoints { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Маркетинговая акция с периодом действия.</summary>
public class Promotion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }
    /// <summary>Бинарные данные картинки акции (хранится в БД).</summary>
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; } = "image/jpeg";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
