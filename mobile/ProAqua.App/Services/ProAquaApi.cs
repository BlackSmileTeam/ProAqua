using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProAqua.App.Services;

public class ProAquaApi
{
    public static string BaseUrl { get; set; } =
#if ANDROID
        "http://10.0.2.2:5080";
#else
        "http://localhost:5080";
#endif

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
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
            var res = await _http.PostAsJsonAsync($"{BaseUrl}/api/auth/login", new { phone, password });
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
            throw new Exception($"Нет связи с сервером ({BaseUrl}). Запустите ПроАква API в VS.");
        }
        catch (TaskCanceledException)
        {
            throw new Exception($"Таймаут связи с сервером ({BaseUrl}).");
        }
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
        => _http.GetFromJsonAsync<List<ServiceItem>>($"{BaseUrl}/api/services", JsonOptions);

    public Task<List<BookingItem>?> GetMyBookingsAsync()
        => _http.GetFromJsonAsync<List<BookingItem>>($"{BaseUrl}/api/bookings/mine", JsonOptions);

    public Task<Profile?> GetProfileAsync()
        => _http.GetFromJsonAsync<Profile>($"{BaseUrl}/api/me", JsonOptions);

    public async Task UpdateProfileAsync(string? name)
    {
        var res = await _http.PutAsJsonAsync($"{BaseUrl}/api/me", new { name });
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception(TryMessage(body));
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
            throw new Exception(TryMessage(body));

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

    public async Task<BookingItem> CreateBookingAsync(Guid serviceId, DateTime startAt)
    {
        var res = await _http.PostAsJsonAsync($"{BaseUrl}/api/bookings", new { serviceId, startAt });
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception(TryMessage(body));
        return JsonSerializer.Deserialize<BookingItem>(body, JsonOptions)!;
    }

    public async Task RepeatBookingAsync(Guid bookingId, DateTime startAt)
    {
        var res = await _http.PostAsJsonAsync($"{BaseUrl}/api/bookings/{bookingId}/repeat", new { serviceId = Guid.Empty, startAt });
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception(TryMessage(body));
    }
}

public record AuthResponse(string Token, Guid UserId, string Phone, string? Name, string Role, string ReferralCode, int LoyaltyPoints, int LoyaltyLevel, bool MustChangePassword);
public record ServiceItem(Guid Id, string Title, string Description, string Category, int DurationMinutes, decimal PriceFrom, string? ImageUrl, string? BeforeAfterImageUrl);
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
