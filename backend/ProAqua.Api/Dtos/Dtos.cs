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

public record RegisterStaffDto(string Phone, string Password, string? Name, string Role);

public record ServiceDto(Guid Id, string Title, string Description, string Category, int DurationMinutes, decimal PriceFrom, string? ImageUrl, string? BeforeAfterImageUrl);
public record CreateServiceDto(string Title, string Description, string Category, int DurationMinutes, decimal PriceFrom, string? ImageUrl, string? BeforeAfterImageUrl, int SortOrder);

public record CreateBookingDto(Guid ServiceId, DateTime StartAt, Guid? VehicleId, string? Comment);
public record BookingDto(Guid Id, Guid ServiceId, string ServiceTitle, DateTime StartAt, DateTime EndAt, string Status, decimal? FinalPrice, string? Comment);
public record UpdateBookingStatusDto(BookingStatus Status);

public record VehicleDto(Guid Id, string Brand, string Model, string? PlateNumber, VehicleType Type);
public record UpsertVehicleDto(string Brand, string Model, string? PlateNumber, VehicleType Type);

public record RegisterDeviceDto(string Token, string Platform);
public record UpdateProfileDto(string? Name, string? AvatarUrl);

public record SlotDto(DateTime StartAt, bool Available);
public record WorkBayDto(Guid Id, string Name, bool IsActive);
public record CreateWorkBayDto(string Name);
