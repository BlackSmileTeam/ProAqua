using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProAqua.App.Services;

public class ProAquaApi
{
    // Production: direct backend API (BACKEND_PORT 55511). Paths are /api/... on the API itself.
    // Via admin nginx (55512) also works for /api; local: "http://10.0.2.2:5080" / "http://localhost:5080".
    public static string BaseUrl { get; set; } = "http://139.100.225.234:55511";

    private readonly HttpClient _http = new()
    {
        // Keep short so splash/home do not hang when the API is down.
        Timeout = TimeSpan.FromSeconds(12)
    };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string? Token { get; private set; }

    public void SetToken(string? token)
    {
        Token = token;
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
        if (token is not null)
            Preferences.Default.Set("token", token);
        else
        {
            Preferences.Default.Remove("token");
            Preferences.Default.Remove("must_change_password");
        }
    }

    public void RestoreSession()
    {
        var token = Preferences.Default.Get("token", string.Empty);
        if (!string.IsNullOrWhiteSpace(token))
            SetToken(token);
    }

    public bool MustChangePassword => Preferences.Default.Get("must_change_password", false);

    public async Task<AuthResponse> LoginAsync(string phone, string password)
    {
        try
        {
            var normalizedPhone = NormalizePhone(phone);
            var normalizedPassword = (password ?? string.Empty).Trim();
            var res = await _http.PostAsJsonAsync($"{BaseUrl}/api/auth/login", new { phone = normalizedPhone, password = normalizedPassword });
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                throw new Exception(TryMessage(body) ?? $"Ошибка входа ({(int)res.StatusCode})");
            var data = JsonSerializer.Deserialize<AuthResponse>(body, JsonOptions)!;
            SetToken(data.Token);
            Preferences.Default.Set("must_change_password", data.MustChangePassword);
            return data;
        }
        catch (HttpRequestException)
        {
            throw new Exception("Нет связи");
        }
        catch (TaskCanceledException)
        {
            throw new Exception("Нет связи");
        }
    }

    private static string NormalizePhone(string phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith('8'))
            digits = "7" + digits[1..];
        if (digits.Length == 10)
            digits = "7" + digits;
        return digits.Length == 0 ? string.Empty : "+" + digits;
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var res = await _http.PostAsJsonAsync($"{BaseUrl}/api/auth/change-password", new { currentPassword, newPassword });
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception(TryMessage(body) ?? body);
        Preferences.Default.Set("must_change_password", false);
    }

    private static string? TryMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m))
            {
                var text = m.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }
        catch { /* ignore */ }

        return "Произошла ошибка. Попробуйте другое время или повторите позже.";
    }

    public Task<List<ServiceItem>?> GetServicesAsync()
        => GetJsonAsync<List<ServiceItem>>($"{BaseUrl}/api/services", needsAuth: false);

    public Task<ServiceDetail?> GetServiceDetailAsync(Guid id)
        => GetJsonAsync<ServiceDetail>($"{BaseUrl}/api/services/{id}", needsAuth: false);

    public Task<List<PromotionItem>?> GetPromotionsAsync()
        => GetJsonAsync<List<PromotionItem>>($"{BaseUrl}/api/promotions", needsAuth: false);

    public Task<List<BookingItem>?> GetMyBookingsAsync()
        => GetJsonAsync<List<BookingItem>>($"{BaseUrl}/api/bookings/mine");

    public Task<Profile?> GetProfileAsync()
        => GetJsonAsync<Profile>($"{BaseUrl}/api/me");

    public async Task UpdateProfileAsync(string? name)
    {
        var res = await _http.PutAsJsonAsync($"{BaseUrl}/api/me", new { name });
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            await ThrowIfHttpErrorAsync(res.StatusCode, body);
    }

    public async Task<string> UploadAvatarAsync(byte[] bytes, string? fileName)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "avatar.jpg" : fileName);

        var res = await _http.PostAsync($"{BaseUrl}/api/me/avatar", content);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            await ThrowIfHttpErrorAsync(res.StatusCode, body);

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("avatarUrl").GetString() ?? string.Empty;
    }

    public ImageSource ResolveMediaSource(string? relativeOrAbsoluteUrl, string fallbackLocal)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsoluteUrl))
            return ImageSource.FromFile(fallbackLocal);

        if (relativeOrAbsoluteUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return ImageSource.FromUri(new Uri(relativeOrAbsoluteUrl));

        var url = $"{BaseUrl.TrimEnd('/')}/{relativeOrAbsoluteUrl.TrimStart('/')}";
        return ImageSource.FromUri(new Uri(url));
    }

    public Task<List<SlotDto>?> GetSlotsAsync(Guid serviceId, DateTime date)
    {
        var d = date.ToString("yyyy-MM-dd");
        return _http.GetFromJsonAsync<List<SlotDto>>($"{BaseUrl}/api/bookings/slots?serviceId={serviceId}&date={d}", JsonOptions);
    }

    public async Task<BookingItem> CreateBookingAsync(Guid serviceId, DateTime startAt, int vehicleType = 0)
    {
        var res = await _http.PostAsJsonAsync($"{BaseUrl}/api/bookings", new { serviceId, startAt, vehicleType });
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            await ThrowIfHttpErrorAsync(res.StatusCode, body);
        return JsonSerializer.Deserialize<BookingItem>(body, JsonOptions)!;
    }

    public async Task RepeatBookingAsync(Guid bookingId, DateTime startAt)
    {
        var res = await _http.PostAsJsonAsync($"{BaseUrl}/api/bookings/{bookingId}/repeat", new { serviceId = Guid.Empty, startAt });
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            await ThrowIfHttpErrorAsync(res.StatusCode, body);
    }

    private async Task<T?> GetJsonAsync<T>(string url, bool needsAuth = true)
    {
        System.Diagnostics.Debug.WriteLine($"[ProAquaApi] GET {url}");
        var started = DateTime.UtcNow;
        HttpResponseMessage res;
        try
        {
            res = await _http.GetAsync(url);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProAquaApi] GET failed {url}: {ex.Message}");
            throw new Exception("Нет связи");
        }
        catch (TaskCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProAquaApi] GET timeout/cancel {url}: {ex.Message}");
            throw new Exception("Нет связи");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProAquaApi] GET failed {url}: {ex.Message}");
            throw;
        }

        var ms = (DateTime.UtcNow - started).TotalMilliseconds;
        var body = await res.Content.ReadAsStringAsync();
        System.Diagnostics.Debug.WriteLine($"[ProAquaApi] GET {url} -> {(int)res.StatusCode} ({ms:0}ms, {body.Length}b)");
        if (!res.IsSuccessStatusCode)
            await ThrowIfHttpErrorAsync(res.StatusCode, body, needsAuth);

        if (string.IsNullOrWhiteSpace(body))
            return default;

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private Task ThrowIfHttpErrorAsync(HttpStatusCode statusCode, string body, bool needsAuth = true)
    {
        if (needsAuth && statusCode == HttpStatusCode.Unauthorized)
        {
            SetToken(null);
            throw new Exception("Сессия истекла. Войдите снова.");
        }

        throw new Exception(TryMessage(body) ?? $"Ошибка сервера ({(int)statusCode})");
    }
}

public record AuthResponse(string Token, Guid UserId, string Phone, string? Name, string Role, string ReferralCode, int LoyaltyPoints, int LoyaltyLevel, bool MustChangePassword);
public record ServiceItem(Guid Id, string Title, string Description, string Category, int DurationMinutes, decimal PriceFrom, string? ImageUrl, string? BeforeAfterImageUrl, string? Purpose = null, string? DetailsHtml = null, bool HasImage = false, bool HasVariants = false);
public record ServiceVariantItem(Guid Id, string Title, string Description, int DurationMinutes, decimal PriceSedan, decimal PriceCrossover, decimal PriceSuv, decimal PriceSuvXl, decimal PriceFrom, string? ImageUrl);
public record ServiceDetail(Guid Id, string Title, string Description, string Category, decimal PriceFrom, string? ImageUrl, string? Purpose, string? DetailsHtml, List<ServiceVariantItem> Variants);
public record PromotionItem(Guid Id, string Title, string Description, DateTime StartsAt, DateTime EndsAt, bool IsActive, string? ImageUrl, bool HasImage = false);
public record BookingItem(Guid Id, Guid ServiceId, string ServiceTitle, DateTime StartAt, DateTime EndAt, string Status, decimal? FinalPrice, string? Comment);
public record SlotDto(DateTime StartAt, bool Available);
public record Profile(
    Guid Id,
    string Phone,
    string? Name,
    string? AvatarUrl,
    string Role,
    string ReferralCode,
    int ReferralCount,
    int LoyaltyPoints,
    int LoyaltyLevel,
    string LevelTitle,
    bool MustChangePassword);
