namespace ProAqua.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Key { get; set; } = "ProAquaDevSecretKey_ChangeMe_32chars!";
    public string Issuer { get; set; } = "ProAquaApi";
    public string Audience { get; set; } = "ProAquaClient";
    public int ExpireDays { get; set; } = 30;
}

public class PushOptions
{
    public const string SectionName = "Push";
    /// <summary>Dev | FcmHttpV1</summary>
    public string Provider { get; set; } = "Dev";
    public string? FcmProjectId { get; set; }
    public string? FcmServiceAccountJsonPath { get; set; }
}

public class AmoCrmOptions
{
    public const string SectionName = "AmoCrm";
    public bool Enabled { get; set; }
    public string? BaseUrl { get; set; }
    public string? AccessToken { get; set; }
    public long? PipelineId { get; set; }
    public long? StatusId { get; set; }
}
