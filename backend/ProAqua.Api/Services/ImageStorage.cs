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
        var baseUrl = $"{request.Scheme}://{request.Host}";
        return baseUrl + relativePath;
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
