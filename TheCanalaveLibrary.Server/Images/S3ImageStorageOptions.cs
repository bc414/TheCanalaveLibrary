namespace TheCanalaveLibrary.Server;

/// <summary>
/// Configuration for <see cref="S3ImageStorageService"/>, bound from <c>ImageStorage:S3</c>.
/// Selected by <c>ImageStorage:Provider = "S3"</c> (Program.cs provider switch; default is
/// <c>Local</c>). Under Aspire the AppHost injects all of these as env vars pointing at the
/// Garage container; in production they point at Cloudflare R2
/// (<c>ServiceUrl = https://&lt;account-id&gt;.r2.cloudflarestorage.com</c>, <c>Region = auto</c>,
/// credentials from an R2 API token). See audit/ImageStorage.md "R2 interchangeability".
/// </summary>
public sealed class S3ImageStorageOptions
{
    public const string SectionName = "ImageStorage:S3";

    public string ServiceUrl { get; set; } = "";

    /// <summary>SigV4 signing region: <c>garage</c> for the dev container (must equal
    /// garage.toml's <c>s3_region</c> — Garage rejects other regions), <c>auto</c> for R2.</summary>
    public string Region { get; set; } = "auto";

    public string AccessKey { get; set; } = "";

    public string SecretKey { get; set; } = "";

    public string BucketName { get; set; } = "";
}
