namespace NorthWind.Membership.Backend.Core.Options;
public class JwtOptions
{
    public const string SectionKey = nameof(JwtOptions);
    public string SecurityKey { get; set; } = string.Empty;
    public string ValidIssuer { get; set; } = string.Empty;
    public string ValidAudience { get; set; } = string.Empty;
    public int ExpireInMinutes { get; set; }
}
