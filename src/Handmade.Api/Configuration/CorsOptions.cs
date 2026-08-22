namespace Handmade.Api.Configuration;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public const string DefaultPolicyName = "Default";

    public string[] AllowedOrigins { get; set; } = [];
}
