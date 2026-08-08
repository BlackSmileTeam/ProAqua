using System.Text.RegularExpressions;

namespace ProAqua.Api.Services;

public static class ImageStorage
{
    private static readonly Regex DataUrl = new(
        @"^data:(?<mime>[\w/+.-]+);base64,(?<data>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public static (byte[] Data, string ContentType)? TryDecode(string? base64OrDataUrl, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(base64OrDataUrl))
            return null;

        var raw = base64OrDataUrl.Trim();
        var mime = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType.Trim();
        var m = DataUrl.Match(raw);
        if (m.Success)
        {
            mime = m.Groups["mime"].Value;
            raw = m.Groups["data"].Value;
        }

        try
        {
            var bytes = Convert.FromBase64String(raw);
            return bytes.Length == 0 ? null : (bytes, mime);
        }
        catch
        {
            return null;
        }
    }

    public static string AbsoluteImageUrl(HttpRequest request, string relativePath)
    {
        // Prefer public host from reverse proxy (nginx $http_host / X-Forwarded-Host)
        // so image URLs keep the published port (e.g. :55512), not the backend listen port.
        var forwardedHost = request.Headers["X-Forwarded-Host"].FirstOrDefault()
            ?? request.Headers["X-Original-Host"].FirstOrDefault();
        var host = !string.IsNullOrWhiteSpace(forwardedHost)
            ? forwardedHost.Split(',', 2)[0].Trim()
            : request.Host.Value;
        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault()
            ?? request.Scheme;
        if (scheme.Contains(',', StringComparison.Ordinal))
            scheme = scheme.Split(',', 2)[0].Trim();
        return $"{scheme}://{host}{relativePath}";
    }

    public static byte[]? ReadSeedFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Database", "seed-images", fileName);
        if (!File.Exists(path))
        {
            // during development: project folder relative to content root
            path = Path.Combine(Directory.GetCurrentDirectory(), "Database", "seed-images", fileName);
        }
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
