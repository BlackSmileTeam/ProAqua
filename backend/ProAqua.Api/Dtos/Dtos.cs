using ProAqua.Api.Domain;

namespace ProAqua.Api.Dtos;

public record LoginDto(string Phone, string Password);
public record AuthResponseDto(string Token, Guid UserId, string Phone, string? Name, string Role, string ReferralCode, int LoyaltyPoints, int LoyaltyLevel, bool MustChangePassword);

public record RegisterClientDto(
    string Phone,
    string Password,
    string? Name,
    string? ReferralCode,
    string? VehicleBrand,
    string? VehicleModel,
    string? PlateNumber,
    VehicleType VehicleType = VehicleType.Sedan);

public record ResetPasswordDto(string Password);
public record ChangePasswordDto(string CurrentPassword, string NewPassword);
public record UpdateClientDto(string? Name, string? Phone, int? LoyaltyPoints, int? LoyaltyLevel, bool? IsActive);

public record RegisterStaffDto(string Phone, string Password, string? Name, string Role);

public record ServiceDto(Guid Id, string Title, string Description, string Category, int DurationMinutes, decimal PriceFrom, string? ImageUrl, string? BeforeAfterImageUrl, string? Purpose = null, string? DetailsHtml = null, bool HasImage = false, bool HasVariants = false, int SortOrder = 0, bool IsActive = true);
public record ServiceVariantDto(Guid Id, string Title, string Description, int DurationMinutes, decimal PriceSedan, decimal PriceCrossover, decimal PriceSuv, decimal PriceSuvXl, decimal PriceFrom, string? ImageUrl);
public record ServiceDetailDto(Guid Id, string Title, string Description, string Category, decimal PriceFrom, string? ImageUrl, string? Purpose, string? DetailsHtml, IReadOnlyList<ServiceVariantDto> Variants);
public record CreateServiceDto(string Title, string Description, string Category, int DurationMinutes, decimal PriceFrom, string? ImageUrl, string? BeforeAfterImageUrl, int SortOrder, string? Purpose = null, string? DetailsHtml = null, string? ImageBase64 = null, string? ImageContentType = null, bool IsActive = true);

public record CreateBookingDto(Guid ServiceId, DateTime StartAt, Guid? VehicleId, string? Comment, VehicleType VehicleType = VehicleType.Sedan);
public record BookingDto(Guid Id, Guid ServiceId, string ServiceTitle, DateTime StartAt, DateTime EndAt, string Status, decimal? FinalPrice, string? Comment);
public record UpdateBookingStatusDto(BookingStatus Status);

public record VehicleDto(Guid Id, string Brand, string Model, string? PlateNumber, VehicleType Type);
public record UpsertVehicleDto(string Brand, string Model, string? PlateNumber, VehicleType Type);

public record RegisterDeviceDto(string Token, string Platform);
public record UpdateProfileDto(string? Name, string? AvatarUrl);

public record SlotDto(DateTime StartAt, bool Available);
public record WorkBayDto(Guid Id, string Name, bool IsActive);
public record CreateWorkBayDto(string Name);

public record PromotionDto(Guid Id, string Title, string Description, DateTime StartsAt, DateTime EndsAt, bool IsActive, string? ImageUrl, bool HasImage = false);
public record UpsertPromotionDto(string Title, string Description, DateTime StartsAt, DateTime EndsAt, bool IsActive, string? ImageUrl = null, string? ImageBase64 = null, string? ImageContentType = null);
