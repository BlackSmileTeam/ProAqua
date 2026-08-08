using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ProAqua.Api.Data;
using ProAqua.Api.Domain;
using ProAqua.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ProAqua.Api.Services;

public interface IPushSender
{
    Task SendAsync(IEnumerable<string> tokens, string title, string body, IDictionary<string, string>? data = null, CancellationToken ct = default);
}

public class DevPushSender(ILogger<DevPushSender> logger) : IPushSender
{
    public Task SendAsync(IEnumerable<string> tokens, string title, string body, IDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV PUSH] {Title} | {Body} | tokens={Count}", title, body, tokens.Count());
        return Task.CompletedTask;
    }
}

public class FcmHttpV1PushSender(ILogger<FcmHttpV1PushSender> logger) : IPushSender
{
    public Task SendAsync(IEnumerable<string> tokens, string title, string body, IDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        logger.LogWarning("FcmHttpV1 provider selected but service-account send is stubbed. Title={Title}", title);
        return Task.CompletedTask;
    }
}

public interface IAmoCrmSync
{
    Task SyncBookingAsync(Booking booking, CancellationToken ct = default);
}

public class AmoCrmSyncService(IOptions<AmoCrmOptions> options, IHttpClientFactory httpClientFactory, ILogger<AmoCrmSyncService> logger) : IAmoCrmSync
{
    public async Task SyncBookingAsync(Booking booking, CancellationToken ct = default)
    {
        var cfg = options.Value;
        if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.BaseUrl) || string.IsNullOrWhiteSpace(cfg.AccessToken))
        {
            logger.LogDebug("AmoCRM sync skipped (disabled or not configured)");
            return;
        }

        var client = httpClientFactory.CreateClient("amocrm");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.AccessToken);

        var payload = new
        {
            name = $"Запись ПроАква #{booking.Id.ToString()[..8]}",
            created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            pipeline_id = cfg.PipelineId,
            status_id = cfg.StatusId,
            _embedded = new
            {
                contacts = new[]
                {
                    new { name = booking.User?.Name ?? booking.User?.Phone ?? "Клиент" }
                }
            }
        };

        var response = await client.PostAsJsonAsync($"{cfg.BaseUrl.TrimEnd('/')}/api/v4/leads", new[] { payload }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        logger.LogInformation("AmoCRM lead sync status={Status} body={Body}", response.StatusCode, body);
    }
}

public class AuthService(ProAquaDbContext db, IOptions<JwtOptions> jwtOptions, ILogger<AuthService> logger)
{
    public async Task<(bool ok, string? token, AppUser? user, string message)> LoginAsync(string phone, string password, CancellationToken ct = default)
    {
        var sourcePhone = phone;
        phone = NormalizePhone(phone);
        password = (password ?? string.Empty).Trim();
        if (password.Length < 4)
        {
            logger.LogWarning("Login rejected: short password phone={Phone}", phone);
            return (false, null, null, "Введите пароль");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Phone == phone, ct);
        if (user is null)
        {
            logger.LogWarning("Login rejected: user not found phone={Phone} source={SourcePhone}", phone, sourcePhone);
            return (false, null, null, "Неверный телефон или пароль");
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Login rejected: inactive user userId={UserId} phone={Phone}", user.Id, phone);
            return (false, null, null, "Неверный телефон или пароль");
        }

        if (string.IsNullOrWhiteSpace(user.PinHash) || !BCrypt.Net.BCrypt.Verify(password, user.PinHash))
        {
            logger.LogWarning("Login rejected: password mismatch userId={UserId} phone={Phone}", user.Id, phone);
            return (false, null, null, "Неверный телефон или пароль");
        }

        logger.LogInformation("Login accepted userId={UserId} phone={Phone}", user.Id, phone);
        return (true, CreateJwt(user), user, "OK");
    }

    public async Task<(bool ok, AppUser? user, string message)> RegisterClientAsync(
        string phone,
        string temporaryPassword,
        string? name,
        string? referralCode,
        string? vehicleBrand,
        string? vehicleModel,
        string? plateNumber,
        VehicleType vehicleType,
        CancellationToken ct = default)
    {
        phone = NormalizePhone(phone);
        temporaryPassword = (temporaryPassword ?? string.Empty).Trim();
        if (temporaryPassword.Length < 4)
            return (false, null, "Пароль должен быть не короче 4 символов");

        if (await db.Users.AnyAsync(u => u.Phone == phone, ct))
            return (false, null, "Клиент с таким телефоном уже есть");

        Guid? referredBy = null;
        if (!string.IsNullOrWhiteSpace(referralCode))
        {
            var referrer = await db.Users.FirstOrDefaultAsync(u => u.ReferralCode == referralCode.Trim().ToUpperInvariant(), ct);
            if (referrer is not null)
            {
                referredBy = referrer.Id;
                referrer.LoyaltyPoints += 100;
                db.LoyaltyTransactions.Add(new LoyaltyTransaction
                {
                    UserId = referrer.Id,
                    PointsDelta = 100,
                    Reason = "Реферал: новый клиент"
                });
            }
        }

        var user = new AppUser
        {
            Phone = phone,
            Name = string.IsNullOrWhiteSpace(name) ? "Клиент" : name.Trim(),
            Role = UserRole.Client,
            PinHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
            MustChangePassword = true,
            ReferralCode = await GenerateUniqueReferralCodeAsync(ct),
            ReferredByUserId = referredBy,
            LoyaltyPoints = referredBy.HasValue ? 50 : 0
        };
        db.Users.Add(user);

        if (referredBy.HasValue)
        {
            db.LoyaltyTransactions.Add(new LoyaltyTransaction
            {
                UserId = user.Id,
                PointsDelta = 50,
                Reason = "Бонус за приглашение"
            });
        }

        if (!string.IsNullOrWhiteSpace(vehicleBrand) || !string.IsNullOrWhiteSpace(vehicleModel))
        {
            db.Vehicles.Add(new Vehicle
            {
                UserId = user.Id,
                Brand = vehicleBrand?.Trim() ?? "",
                Model = vehicleModel?.Trim() ?? "",
                PlateNumber = plateNumber?.Trim(),
                Type = vehicleType
            });
        }

        await db.SaveChangesAsync(ct);
        return (true, user, "Клиент зарегистрирован");
    }

    public async Task<(bool ok, string message)> ResetTemporaryPasswordAsync(Guid userId, string temporaryPassword, CancellationToken ct = default)
    {
        temporaryPassword = (temporaryPassword ?? string.Empty).Trim();
        if (temporaryPassword.Length < 4)
            return (false, "Пароль должен быть не короче 4 символов");

        var user = await db.Users.FindAsync([userId], ct);
        if (user is null) return (false, "Пользователь не найден");
        user.PinHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
        user.MustChangePassword = true;
        await db.SaveChangesAsync(ct);
        return (true, "Пароль обновлён");
    }

    public async Task<(bool ok, string message)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        currentPassword = (currentPassword ?? string.Empty).Trim();
        newPassword = (newPassword ?? string.Empty).Trim();
        if (newPassword.Length < 6)
            return (false, "Новый пароль должен быть не короче 6 символов");

        var user = await db.Users.FindAsync([userId], ct);
        if (user is null) return (false, "Пользователь не найден");
        if (string.IsNullOrWhiteSpace(user.PinHash) || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PinHash))
            return (false, "Текущий пароль неверен");

        user.PinHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = false;
        await db.SaveChangesAsync(ct);
        return (true, "Пароль изменён");
    }

    public async Task<(bool ok, AppUser? user, string message)> RegisterStaffAsync(
        string phone,
        string password,
        string? name,
        string roleName,
        CancellationToken ct = default)
    {
        phone = NormalizePhone(phone);
        password = (password ?? string.Empty).Trim();
        if (password.Length < 4)
            return (false, null, "Пароль должен быть не короче 4 символов");

        if (!Enum.TryParse<UserRole>(roleName, true, out var role) || role is not (UserRole.Admin or UserRole.Master))
            return (false, null, "Роль должна быть Admin или Master");

        if (await db.Users.AnyAsync(u => u.Phone == phone, ct))
            return (false, null, "Пользователь с таким телефоном уже есть");

        var user = new AppUser
        {
            Phone = phone,
            Name = string.IsNullOrWhiteSpace(name) ? role.ToString() : name.Trim(),
            Role = role,
            PinHash = BCrypt.Net.BCrypt.HashPassword(password),
            MustChangePassword = false,
            ReferralCode = await GenerateUniqueReferralCodeAsync(ct)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return (true, user, "Сотрудник создан");
    }

    private string CreateJwt(AppUser user)
    {
        var jwt = jwtOptions.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.MobilePhone, user.Phone),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, expires: DateTime.UtcNow.AddDays(jwt.ExpireDays), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GenerateUniqueReferralCodeAsync(CancellationToken ct)
    {
        for (var i = 0; i < 20; i++)
        {
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
            if (!await db.Users.AnyAsync(u => u.ReferralCode == code, ct))
                return code;
        }
        return Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    }

    public static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith('8'))
            digits = "7" + digits[1..];
        if (digits.Length == 10)
            digits = "7" + digits;
        return "+" + digits;
    }
}

public class BookingService(ProAquaDbContext db, IAmoCrmSync amoCrm, IPushSender push)
{
    public async Task<(bool ok, Booking? booking, string message)> CreateAsync(
        Guid userId,
        Guid serviceId,
        DateTime startAt,
        Guid? vehicleId,
        string? comment,
        VehicleType vehicleType = VehicleType.Sedan,
        CancellationToken ct = default)
    {
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive, ct);
        if (service is null) return (false, null, "Услуга не найдена");
        if (service.ParentId is null && await db.Services.AnyAsync(v => v.ParentId == service.Id && v.IsActive, ct))
            return (false, null, "Выберите конкретную подуслугу");

        startAt = DateTime.SpecifyKind(startAt, DateTimeKind.Utc);
        var duration = Math.Max(30, service.DurationMinutes);
        var endAt = startAt.AddMinutes(duration);

        // Block only overlapping bookings in the same service category (completed/cancelled/no-show free the slot).
        var blockingStatuses = new[]
        {
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            BookingStatus.InProgress,
            BookingStatus.Ready
        };
        var overlap = await db.Bookings.AnyAsync(b =>
            blockingStatuses.Contains(b.Status) &&
            b.Service != null &&
            b.Service.Category == service.Category &&
            b.StartAt < endAt && b.EndAt > startAt, ct);
        if (overlap) return (false, null, "Слот уже занят, выберите другое время");

        var bay = await db.WorkBays.FirstOrDefaultAsync(b => b.IsActive, ct);
        var booking = new Booking
        {
            UserId = userId,
            ServiceId = serviceId,
            VehicleId = vehicleId,
            WorkBayId = bay?.Id,
            StartAt = startAt,
            EndAt = endAt,
            Comment = comment,
            Status = BookingStatus.Confirmed,
            FinalPrice = service.PriceFor(vehicleType)
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        booking = await db.Bookings
            .Include(b => b.User)
            .Include(b => b.Service)
            .FirstAsync(b => b.Id == booking.Id, ct);

        await amoCrm.SyncBookingAsync(booking, ct);

        var tokens = await db.DeviceTokens.Where(d => d.UserId == userId).Select(d => d.Token).ToListAsync(ct);
        await push.SendAsync(tokens, "Запись подтверждена", $"{service.Title} — {startAt:dd.MM HH:mm}", new Dictionary<string, string>
        {
            ["bookingId"] = booking.Id.ToString()
        }, ct);

        return (true, booking, "Запись создана");
    }

    public async Task UpdateStatusAsync(Guid bookingId, BookingStatus status, CancellationToken ct = default)
    {
        var booking = await db.Bookings.Include(b => b.Service).Include(b => b.User).FirstOrDefaultAsync(b => b.Id == bookingId, ct)
                      ?? throw new InvalidOperationException("Booking not found");
        booking.Status = status;
        booking.UpdatedAt = DateTime.UtcNow;

        if (status == BookingStatus.Completed && booking.User is not null)
        {
            var points = Math.Max(10, (int)((booking.FinalPrice ?? 0) / 100));
            booking.User.LoyaltyPoints += points;
            booking.User.LoyaltyLevel = booking.User.LoyaltyPoints switch
            {
                >= 2000 => 3,
                >= 500 => 2,
                _ => 1
            };
            db.LoyaltyTransactions.Add(new LoyaltyTransaction
            {
                UserId = booking.UserId,
                PointsDelta = points,
                Reason = "Завершение визита",
                BookingId = booking.Id
            });
        }

        await db.SaveChangesAsync(ct);

        var tokens = await db.DeviceTokens.Where(d => d.UserId == booking.UserId).Select(d => d.Token).ToListAsync(ct);
        var title = status switch
        {
            BookingStatus.InProgress => "Авто в работе",
            BookingStatus.Ready => "Готово к выдаче",
            BookingStatus.Completed => "Визит завершён",
            BookingStatus.Cancelled => "Запись отменена",
            _ => "Статус обновлён"
        };
        await push.SendAsync(tokens, title, booking.Service?.Title ?? "ПроАква", new Dictionary<string, string>
        {
            ["bookingId"] = booking.Id.ToString(),
            ["status"] = status.ToString()
        }, ct);
    }
}

public static class AnalyticsService
{
    public static object Build(ProAquaDbContext db)
    {
        var now = DateTime.UtcNow;
        var from = now.AddDays(-30);
        var bookings = db.Bookings.AsNoTracking().Where(b => b.CreatedAt >= from).ToList();
        var completed = bookings.Count(b => b.Status == BookingStatus.Completed);
        var noShow = bookings.Count(b => b.Status == BookingStatus.NoShow);
        var totalRelevant = completed + noShow;

        var users = db.Users.AsNoTracking().Where(u => u.Role == UserRole.Client).ToList();
        var referredIds = users.Where(u => u.ReferredByUserId != null).Select(u => u.Id).ToHashSet();
        var referred = referredIds.Count;

        var referredCompleted = referred == 0
            ? 0
            : db.Bookings.AsNoTracking().Count(b =>
                b.Status == BookingStatus.Completed && referredIds.Contains(b.UserId));

        decimal avgLtv = 0;
        if (users.Count > 0)
        {
            var totals = db.Bookings.AsNoTracking()
                .Where(b => b.Status == BookingStatus.Completed && b.FinalPrice != null)
                .GroupBy(b => b.UserId)
                .Select(g => g.Sum(x => x.FinalPrice ?? 0))
                .ToList();
            avgLtv = totals.Count == 0 ? 0 : totals.Average();
        }

        return new
        {
            periodDays = 30,
            bookingsTotal = bookings.Count,
            completed,
            noShowRate = totalRelevant == 0 ? 0 : Math.Round(100.0 * noShow / totalRelevant, 1),
            referralSignups = referred,
            referralConversionPercent = referred == 0 ? 0 : Math.Round(100.0 * referredCompleted / referred, 1),
            averageLtv = Math.Round(avgLtv, 0),
            loyaltyUsers = users.Count(u => u.LoyaltyPoints > 0)
        };
    }
}
