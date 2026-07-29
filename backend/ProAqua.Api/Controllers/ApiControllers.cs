using System.Security.Claims;
using ProAqua.Api.Data;
using ProAqua.Api.Domain;
using ProAqua.Api.Dtos;
using ProAqua.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProAqua.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        var phone = dto.Phone ?? string.Empty;
        var normalizedPhone = AuthService.NormalizePhone(phone);
        logger.LogInformation("Login attempt phone={Phone} ip={Ip}", normalizedPhone, HttpContext.Connection.RemoteIpAddress?.ToString());

        var (ok, token, user, message) = await auth.LoginAsync(phone, dto.Password, ct);
        if (!ok || user is null || token is null)
        {
            logger.LogWarning("Login failed phone={Phone} reason={Reason}", normalizedPhone, message);
            return BadRequest(new { message });
        }

        logger.LogInformation("Login success userId={UserId} phone={Phone} role={Role}", user.Id, user.Phone, user.Role);
        return Ok(new AuthResponseDto(token, user.Id, user.Phone, user.Name, user.Role.ToString(), user.ReferralCode, user.LoyaltyPoints, user.LoyaltyLevel, user.MustChangePassword));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
        var (ok, message) = await auth.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword, ct);
        return ok ? Ok(new { message }) : BadRequest(new { message });
    }
}

[ApiController]
[Route("api/services")]
public class ServicesController(ProAquaDbContext db, ILogger<ServicesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceDto>>> List(CancellationToken ct)
    {
        logger.LogInformation("GET /api/services ip={Ip}", HttpContext.Connection.RemoteIpAddress);
        var rows = await db.Services.AsNoTracking()
            .Where(s => s.IsActive && s.ParentId == null)
            .OrderBy(s => s.SortOrder)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.Description,
                s.Category,
                s.DurationMinutes,
                s.PriceFrom,
                s.ImageUrl,
                s.BeforeAfterImageUrl,
                s.Purpose,
                s.DetailsHtml,
                HasImage = s.ImageData != null && s.ImageData.Length > 0,
                HasVariants = db.Services.Any(v => v.ParentId == s.Id && v.IsActive)
            })
            .ToListAsync(ct);

        var items = rows.Select(s =>
        {
            var url = s.HasImage
                ? ImageStorage.AbsoluteImageUrl(Request, $"/api/services/{s.Id}/image")
                : AbsoluteOrNull(s.ImageUrl);
            return new ServiceDto(s.Id, s.Title, s.Description, s.Category, s.DurationMinutes, s.PriceFrom, url, AbsoluteOrNull(s.BeforeAfterImageUrl), s.Purpose, s.DetailsHtml, s.HasImage, s.HasVariants);
        });
        logger.LogInformation("GET /api/services -> {Count} items", items.Count());
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ServiceDetailDto>> Get(Guid id, CancellationToken ct)
    {
        logger.LogInformation("GET /api/services/{ServiceId} ip={Ip}", id, HttpContext.Connection.RemoteIpAddress);
        var parent = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && s.IsActive, ct);
        if (parent is null)
        {
            logger.LogWarning("GET /api/services/{ServiceId} -> not found (inactive or missing)", id);
            return NotFound();
        }

        // If a variant id was passed, resolve to its parent for detail view
        if (parent.ParentId is not null)
        {
            parent = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == parent.ParentId && s.IsActive, ct);
            if (parent is null) return NotFound();
        }

        var variants = await db.Services.AsNoTracking()
            .Where(v => v.ParentId == parent.Id && v.IsActive)
            .OrderBy(v => v.SortOrder)
            .ToListAsync(ct);

        var parentUrl = parent.ImageData is { Length: > 0 }
            ? ImageStorage.AbsoluteImageUrl(Request, $"/api/services/{parent.Id}/image")
            : AbsoluteOrNull(parent.ImageUrl);

        var variantDtos = variants.Select(v =>
        {
            var url = v.ImageData is { Length: > 0 }
                ? ImageStorage.AbsoluteImageUrl(Request, $"/api/services/{v.Id}/image")
                : AbsoluteOrNull(v.ImageUrl);
            return new ServiceVariantDto(
                v.Id,
                v.Title,
                v.Description,
                v.DurationMinutes,
                v.PriceSedan ?? v.PriceFrom,
                v.PriceCrossover ?? v.PriceFrom,
                v.PriceSuv ?? v.PriceFrom,
                v.PriceSuvXl ?? v.PriceFrom,
                v.PriceFrom,
                url);
        }).ToList();

        logger.LogInformation("GET /api/services/{ServiceId} -> {Title}, variants={Count}", parent.Id, parent.Title, variantDtos.Count);
        return Ok(new ServiceDetailDto(parent.Id, parent.Title, parent.Description, parent.Category, parent.PriceFrom, parentUrl, parent.Purpose, parent.DetailsHtml, variantDtos));
    }

    [HttpGet("{id:guid}/image")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Image(Guid id, CancellationToken ct)
    {
        logger.LogDebug("GET /api/services/{ServiceId}/image ip={Ip}", id, HttpContext.Connection.RemoteIpAddress);
        var row = await db.Services.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.ImageData, s.ImageContentType })
            .FirstOrDefaultAsync(ct);
        if (row?.ImageData is null || row.ImageData.Length == 0)
            return NotFound();
        return File(row.ImageData, row.ImageContentType ?? "image/jpeg");
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<ServiceDto>>> ListAll(CancellationToken ct)
    {
        var rows = await db.Services.AsNoTracking()
            .Where(s => s.ParentId == null)
            .OrderBy(s => s.SortOrder)
            .Select(s => new { s.Id, s.Title, s.Description, s.Category, s.DurationMinutes, s.PriceFrom, s.ImageUrl, s.BeforeAfterImageUrl, s.Purpose, s.DetailsHtml, s.SortOrder, s.IsActive, HasImage = s.ImageData != null && s.ImageData.Length > 0, HasVariants = db.Services.Any(v => v.ParentId == s.Id) })
            .ToListAsync(ct);
        var items = rows.Select(s =>
        {
            var url = s.HasImage
                ? ImageStorage.AbsoluteImageUrl(Request, $"/api/services/{s.Id}/image")
                : AbsoluteOrNull(s.ImageUrl);
            return new ServiceDto(s.Id, s.Title, s.Description, s.Category, s.DurationMinutes, s.PriceFrom, url, AbsoluteOrNull(s.BeforeAfterImageUrl), s.Purpose, s.DetailsHtml, s.HasImage, s.HasVariants, s.SortOrder, s.IsActive);
        });
        return Ok(items);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ServiceDto>> Create([FromBody] CreateServiceDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Укажите название услуги" });

        var entity = new WashService
        {
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "wash" : dto.Category.Trim(),
            DurationMinutes = dto.DurationMinutes,
            PriceFrom = dto.PriceFrom,
            ImageUrl = dto.ImageUrl,
            BeforeAfterImageUrl = dto.BeforeAfterImageUrl,
            Purpose = dto.Purpose?.Trim(),
            DetailsHtml = dto.DetailsHtml,
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive
        };
        ApplyImage(entity, dto.ImageBase64, dto.ImageContentType);
        db.Services.Add(entity);
        await db.SaveChangesAsync(ct);
        var url = entity.ImageData is { Length: > 0 }
            ? ImageStorage.AbsoluteImageUrl(Request, $"/api/services/{entity.Id}/image")
            : AbsoluteOrNull(entity.ImageUrl);
        return Ok(new ServiceDto(entity.Id, entity.Title, entity.Description, entity.Category, entity.DurationMinutes, entity.PriceFrom, url, AbsoluteOrNull(entity.BeforeAfterImageUrl), entity.Purpose, entity.DetailsHtml, entity.ImageData is { Length: > 0 }, false, entity.SortOrder, entity.IsActive));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateServiceDto dto, CancellationToken ct)
    {
        var entity = await db.Services.FindAsync([id], ct);
        if (entity is null) return NotFound();
        entity.Title = dto.Title.Trim();
        entity.Description = dto.Description?.Trim() ?? string.Empty;
        entity.Category = string.IsNullOrWhiteSpace(dto.Category) ? entity.Category : dto.Category.Trim();
        entity.DurationMinutes = dto.DurationMinutes;
        entity.PriceFrom = dto.PriceFrom;
        entity.Purpose = dto.Purpose?.Trim();
        entity.DetailsHtml = dto.DetailsHtml;
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        if (!string.IsNullOrWhiteSpace(dto.ImageBase64))
            ApplyImage(entity, dto.ImageBase64, dto.ImageContentType);
        else if (dto.ImageUrl is not null)
            entity.ImageUrl = dto.ImageUrl;
        if (dto.BeforeAfterImageUrl is not null)
            entity.BeforeAfterImageUrl = dto.BeforeAfterImageUrl;
        await db.SaveChangesAsync(ct);
        return Ok();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await db.Services.FindAsync([id], ct);
        if (entity is null) return NotFound();
        db.Services.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Ok();
    }

    private static void ApplyImage(WashService entity, string? base64, string? contentType)
    {
        var decoded = ImageStorage.TryDecode(base64, contentType);
        if (decoded is null) return;
        entity.ImageData = decoded.Value.Data;
        entity.ImageContentType = decoded.Value.ContentType;
        entity.ImageUrl = null;
    }

    private string? AbsoluteOrNull(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return url;
        return ImageStorage.AbsoluteImageUrl(Request, url.StartsWith('/') ? url : "/" + url);
    }
}

[ApiController]
[Authorize]
[Route("api/bookings")]
public class BookingsController(ProAquaDbContext db, BookingService bookings) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<BookingDto>>> Mine(CancellationToken ct)
    {
        var items = await db.Bookings.Include(b => b.Service)
            .Where(b => b.UserId == UserId)
            .OrderByDescending(b => b.StartAt)
            .Select(b => new BookingDto(b.Id, b.ServiceId, b.Service!.Title, b.StartAt, b.EndAt, b.Status.ToString(), b.FinalPrice, b.Comment))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto, CancellationToken ct)
    {
        var (ok, booking, message) = await bookings.CreateAsync(UserId, dto.ServiceId, dto.StartAt, dto.VehicleId, dto.Comment, dto.VehicleType, ct);
        if (!ok || booking is null) return BadRequest(new { message });
        return Ok(new BookingDto(booking.Id, booking.ServiceId, booking.Service!.Title, booking.StartAt, booking.EndAt, booking.Status.ToString(), booking.FinalPrice, booking.Comment));
    }

    [HttpPost("{id:guid}/repeat")]
    public async Task<IActionResult> Repeat(Guid id, [FromBody] CreateBookingDto dto, CancellationToken ct)
    {
        var previous = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id && b.UserId == UserId, ct);
        if (previous is null) return NotFound();
        var (ok, booking, message) = await bookings.CreateAsync(UserId, previous.ServiceId, dto.StartAt, previous.VehicleId, previous.Comment, VehicleType.Sedan, ct);
        if (!ok || booking is null) return BadRequest(new { message });
        return Ok(new BookingDto(booking.Id, booking.ServiceId, booking.Service!.Title, booking.StartAt, booking.EndAt, booking.Status.ToString(), booking.FinalPrice, booking.Comment));
    }

    [HttpGet("slots")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<SlotDto>>> Slots([FromQuery] DateTime date, [FromQuery] Guid serviceId, CancellationToken ct)
    {
        var service = await db.Services.FindAsync([serviceId], ct);
        if (service is null) return NotFound();
        var day = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var slots = new List<SlotDto>();
        for (var hour = 9; hour <= 20; hour++)
        {
            var start = day.AddHours(hour);
            var end = start.AddMinutes(service.DurationMinutes);
            var busy = await db.Bookings.AnyAsync(b => b.Status != BookingStatus.Cancelled && b.StartAt < end && b.EndAt > start, ct);
            slots.Add(new SlotDto(start, !busy));
        }
        return Ok(slots);
    }
}

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController(ProAquaDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var user = await db.Users.FindAsync([UserId], ct);
        if (user is null) return NotFound();
        var referralCount = await db.Users.CountAsync(u => u.ReferredByUserId == UserId, ct);
        return Ok(new
        {
            user.Id,
            user.Phone,
            user.Name,
            user.AvatarUrl,
            Role = user.Role.ToString(),
            user.ReferralCode,
            ReferralCount = referralCount,
            user.LoyaltyPoints,
            user.LoyaltyLevel,
            LevelTitle = user.LoyaltyLevel switch { 3 => "Платина", 2 => "Серебро", _ => "Гость" },
            user.MustChangePassword
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileDto dto, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([UserId], ct);
        if (user is null) return NotFound();
        if (dto.Name is not null)
            user.Name = dto.Name;
        if (dto.AvatarUrl is not null)
            user.AvatarUrl = dto.AvatarUrl;
        await db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new { message = "Файл пустой" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
            return BadRequest(new { message = "Допустимы JPG, PNG, WEBP" });

        var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
        Directory.CreateDirectory(dir);
        var fileName = $"{UserId}{ext}";
        var path = Path.Combine(dir, fileName);
        await using (var stream = System.IO.File.Create(path))
            await file.CopyToAsync(stream, ct);

        var user = await db.Users.FindAsync([UserId], ct);
        if (user is null) return NotFound();
        user.AvatarUrl = $"/uploads/avatars/{fileName}";
        await db.SaveChangesAsync(ct);
        return Ok(new { avatarUrl = user.AvatarUrl });
    }

    [HttpGet("vehicles")]
    public async Task<ActionResult<IEnumerable<VehicleDto>>> Vehicles(CancellationToken ct)
    {
        var items = await db.Vehicles.Where(v => v.UserId == UserId)
            .Select(v => new VehicleDto(v.Id, v.Brand, v.Model, v.PlateNumber, v.Type))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("vehicles")]
    public async Task<ActionResult<VehicleDto>> AddVehicle([FromBody] UpsertVehicleDto dto, CancellationToken ct)
    {
        var v = new Vehicle
        {
            UserId = UserId,
            Brand = dto.Brand,
            Model = dto.Model,
            PlateNumber = dto.PlateNumber,
            Type = dto.Type
        };
        db.Vehicles.Add(v);
        await db.SaveChangesAsync(ct);
        return Ok(new VehicleDto(v.Id, v.Brand, v.Model, v.PlateNumber, v.Type));
    }

    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceDto dto, CancellationToken ct)
    {
        var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == UserId && d.Token == dto.Token, ct);
        if (existing is null)
        {
            db.DeviceTokens.Add(new DeviceToken { UserId = UserId, Token = dto.Token, Platform = dto.Platform });
        }
        else
        {
            existing.UpdatedAt = DateTime.UtcNow;
            existing.Platform = dto.Platform;
        }
        await db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpGet("loyalty")]
    public async Task<IActionResult> Loyalty(CancellationToken ct)
    {
        var user = await db.Users.FindAsync([UserId], ct);
        if (user is null) return NotFound();
        var history = await db.LoyaltyTransactions.Where(t => t.UserId == UserId)
            .OrderByDescending(t => t.CreatedAt).Take(50)
            .Select(t => new { t.PointsDelta, t.Reason, t.CreatedAt })
            .ToListAsync(ct);
        return Ok(new { user.LoyaltyPoints, user.LoyaltyLevel, history });
    }
}

[ApiController]
[Route("api/promotions")]
public class PromotionsController(ProAquaDbContext db) : ControllerBase
{
    private sealed record PromoRow(Guid Id, string Title, string Description, DateTime StartsAt, DateTime EndsAt, bool IsActive, string? ImageUrl, bool HasImage);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PromotionDto>>> ListActive(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await db.Promotions.AsNoTracking()
            .Where(p => p.IsActive && p.StartsAt <= now && p.EndsAt >= now)
            .OrderBy(p => p.EndsAt)
            .Select(p => new PromoRow(p.Id, p.Title, p.Description, p.StartsAt, p.EndsAt, p.IsActive, p.ImageUrl, p.ImageData != null && p.ImageData.Length > 0))
            .ToListAsync(ct);
        return Ok(rows.Select(Map));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<PromotionDto>>> ListAll(CancellationToken ct)
    {
        var rows = await db.Promotions.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PromoRow(p.Id, p.Title, p.Description, p.StartsAt, p.EndsAt, p.IsActive, p.ImageUrl, p.ImageData != null && p.ImageData.Length > 0))
            .ToListAsync(ct);
        return Ok(rows.Select(Map));
    }

    [HttpGet("{id:guid}/image")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Image(Guid id, CancellationToken ct)
    {
        var row = await db.Promotions.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.ImageData, p.ImageContentType })
            .FirstOrDefaultAsync(ct);
        if (row?.ImageData is null || row.ImageData.Length == 0)
            return NotFound();
        return File(row.ImageData, row.ImageContentType ?? "image/jpeg");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<PromotionDto>> Create([FromBody] UpsertPromotionDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Укажите название акции" });
        if (dto.EndsAt < dto.StartsAt)
            return BadRequest(new { message = "Дата окончания раньше даты начала" });

        var entity = new Promotion
        {
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            StartsAt = DateTime.SpecifyKind(dto.StartsAt, DateTimeKind.Utc),
            EndsAt = DateTime.SpecifyKind(dto.EndsAt, DateTimeKind.Utc),
            IsActive = dto.IsActive,
            ImageUrl = dto.ImageUrl
        };
        ApplyImage(entity, dto.ImageBase64, dto.ImageContentType);
        db.Promotions.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(Map(new PromoRow(entity.Id, entity.Title, entity.Description, entity.StartsAt, entity.EndsAt, entity.IsActive, entity.ImageUrl, entity.ImageData is { Length: > 0 })));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertPromotionDto dto, CancellationToken ct)
    {
        var entity = await db.Promotions.FindAsync([id], ct);
        if (entity is null) return NotFound();
        if (dto.EndsAt < dto.StartsAt)
            return BadRequest(new { message = "Дата окончания раньше даты начала" });

        entity.Title = dto.Title.Trim();
        entity.Description = dto.Description?.Trim() ?? string.Empty;
        entity.StartsAt = DateTime.SpecifyKind(dto.StartsAt, DateTimeKind.Utc);
        entity.EndsAt = DateTime.SpecifyKind(dto.EndsAt, DateTimeKind.Utc);
        entity.IsActive = dto.IsActive;
        if (!string.IsNullOrWhiteSpace(dto.ImageBase64))
            ApplyImage(entity, dto.ImageBase64, dto.ImageContentType);
        else if (dto.ImageUrl is not null)
            entity.ImageUrl = dto.ImageUrl;
        await db.SaveChangesAsync(ct);
        return Ok();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await db.Promotions.FindAsync([id], ct);
        if (entity is null) return NotFound();
        db.Promotions.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Ok();
    }

    private PromotionDto Map(PromoRow p)
    {
        var url = p.HasImage
            ? ImageStorage.AbsoluteImageUrl(Request, $"/api/promotions/{p.Id}/image")
            : AbsoluteOrNull(p.ImageUrl);
        return new PromotionDto(p.Id, p.Title, p.Description, p.StartsAt, p.EndsAt, p.IsActive, url, p.HasImage);
    }

    private static void ApplyImage(Promotion entity, string? base64, string? contentType)
    {
        var decoded = ImageStorage.TryDecode(base64, contentType);
        if (decoded is null) return;
        entity.ImageData = decoded.Value.Data;
        entity.ImageContentType = decoded.Value.ContentType;
        entity.ImageUrl = null;
    }

    private string? AbsoluteOrNull(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return url;
        return ImageStorage.AbsoluteImageUrl(Request, url.StartsWith('/') ? url : "/" + url);
    }
}

[ApiController]
[Authorize(Roles = "Admin,Master")]
[Route("api/admin")]
public class AdminController(ProAquaDbContext db, BookingService bookings, AuthService auth) : ControllerBase
{
    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings([FromQuery] DateTime? date, CancellationToken ct)
    {
        var q = db.Bookings.Include(b => b.Service).Include(b => b.User).Include(b => b.WorkBay).AsQueryable();
        if (date.HasValue)
        {
            var day = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
            q = q.Where(b => b.StartAt >= day && b.StartAt < day.AddDays(1));
        }
        var items = await q.OrderBy(b => b.StartAt).Select(b => new
        {
            b.Id,
            Client = b.User!.Name ?? b.User.Phone,
            Service = b.Service!.Title,
            Bay = b.WorkBay != null ? b.WorkBay.Name : null,
            b.StartAt,
            b.EndAt,
            Status = b.Status.ToString(),
            b.FinalPrice
        }).ToListAsync(ct);
        return Ok(items);
    }

    [HttpPatch("bookings/{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateBookingStatusDto dto, CancellationToken ct)
    {
        await bookings.UpdateStatusAsync(id, dto.Status, ct);
        return Ok();
    }

    [HttpGet("bays")]
    public async Task<ActionResult<IEnumerable<WorkBayDto>>> Bays(CancellationToken ct)
        => Ok(await db.WorkBays.Select(b => new WorkBayDto(b.Id, b.Name, b.IsActive)).ToListAsync(ct));

    [Authorize(Roles = "Admin")]
    [HttpPost("bays")]
    public async Task<IActionResult> CreateBay([FromBody] CreateWorkBayDto dto, CancellationToken ct)
    {
        var bay = new WorkBay { Name = dto.Name };
        db.WorkBays.Add(bay);
        await db.SaveChangesAsync(ct);
        return Ok(new WorkBayDto(bay.Id, bay.Name, bay.IsActive));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("analytics")]
    public IActionResult Analytics() => Ok(AnalyticsService.Build(db));

    [Authorize(Roles = "Admin")]
    [HttpGet("clients")]
    public async Task<IActionResult> Clients(CancellationToken ct)
    {
        var items = await db.Users.Where(u => u.Role == UserRole.Client)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id,
                u.Phone,
                u.Name,
                u.LoyaltyPoints,
                u.LoyaltyLevel,
                LevelTitle = u.LoyaltyLevel >= 3 ? "Платина" : u.LoyaltyLevel == 2 ? "Серебро" : "Гость",
                u.ReferralCode,
                ReferralCount = db.Users.Count(r => r.ReferredByUserId == u.Id),
                u.IsActive,
                u.CreatedAt
            })
            .Take(200)
            .ToListAsync(ct);
        return Ok(items);
    }

    /// <summary>Регистрация клиента при личном визите на мойку.</summary>
    [Authorize(Roles = "Admin,Master")]
    [HttpPost("clients")]
    public async Task<IActionResult> RegisterClient([FromBody] RegisterClientDto dto, CancellationToken ct)
    {
        var (ok, user, message) = await auth.RegisterClientAsync(
            dto.Phone, dto.Password, dto.Name, dto.ReferralCode,
            dto.VehicleBrand, dto.VehicleModel, dto.PlateNumber, dto.VehicleType, ct);
        if (!ok || user is null) return BadRequest(new { message });
        return Ok(new
        {
            user.Id,
            user.Phone,
            user.Name,
            user.ReferralCode,
            user.LoyaltyPoints,
            user.LoyaltyLevel,
            message
        });
    }

    [Authorize(Roles = "Admin,Master")]
    [HttpPut("clients/{id:guid}")]
    public async Task<IActionResult> UpdateClient(Guid id, [FromBody] UpdateClientDto dto, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Client, ct);
        if (user is null) return NotFound(new { message = "Клиент не найден" });

        if (dto.Name is not null)
            user.Name = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name.Trim();

        if (dto.Phone is not null)
        {
            var phone = AuthService.NormalizePhone(dto.Phone);
            if (string.IsNullOrWhiteSpace(phone))
                return BadRequest(new { message = "Некорректный телефон" });
            var taken = await db.Users.AnyAsync(u => u.Phone == phone && u.Id != id, ct);
            if (taken)
                return BadRequest(new { message = "Телефон уже используется" });
            user.Phone = phone;
        }

        if (dto.LoyaltyPoints is not null)
            user.LoyaltyPoints = Math.Max(0, dto.LoyaltyPoints.Value);

        if (dto.LoyaltyLevel is not null)
            user.LoyaltyLevel = Math.Clamp(dto.LoyaltyLevel.Value, 0, 3);
        else if (dto.LoyaltyPoints is not null)
        {
            user.LoyaltyLevel = user.LoyaltyPoints switch
            {
                >= 2000 => 3,
                >= 500 => 2,
                _ => 1
            };
        }

        if (dto.IsActive is not null)
            user.IsActive = dto.IsActive.Value;

        await db.SaveChangesAsync(ct);
        return Ok(new
        {
            user.Id,
            user.Phone,
            user.Name,
            user.LoyaltyPoints,
            user.LoyaltyLevel,
            user.ReferralCode,
            user.IsActive,
            message = "Профиль обновлён"
        });
    }

    [Authorize(Roles = "Admin,Master")]
    [HttpPost("clients/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordDto dto, CancellationToken ct)
    {
        var (ok, message) = await auth.ResetTemporaryPasswordAsync(id, dto.Password, ct);
        return ok ? Ok(new { message }) : BadRequest(new { message });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("staff")]
    public async Task<IActionResult> Staff(CancellationToken ct)
    {
        var items = await db.Users.Where(u => u.Role == UserRole.Admin || u.Role == UserRole.Master)
            .OrderByDescending(u => u.Role)
            .ThenBy(u => u.Name)
            .Select(u => new { u.Id, u.Phone, u.Name, Role = u.Role.ToString(), u.IsActive, u.CreatedAt })
            .ToListAsync(ct);
        return Ok(items);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("staff")]
    public async Task<IActionResult> RegisterStaff([FromBody] RegisterStaffDto dto, CancellationToken ct)
    {
        var (ok, user, message) = await auth.RegisterStaffAsync(dto.Phone, dto.Password, dto.Name, dto.Role, ct);
        if (!ok || user is null) return BadRequest(new { message });
        return Ok(new { user.Id, user.Phone, user.Name, Role = user.Role.ToString(), message });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("staff/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetStaffPassword(Guid id, [FromBody] ResetPasswordDto dto, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && (u.Role == UserRole.Admin || u.Role == UserRole.Master), ct);
        if (user is null) return NotFound(new { message = "Сотрудник не найден" });
        var (ok, message) = await auth.ResetTemporaryPasswordAsync(id, dto.Password, ct);
        if (ok)
        {
            user.MustChangePassword = false;
            await db.SaveChangesAsync(ct);
        }
        return ok ? Ok(new { message = "Пароль обновлён" }) : BadRequest(new { message });
    }
}
