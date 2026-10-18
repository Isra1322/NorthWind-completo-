namespace NorthWind.Membership.Backend.AspNetIdentity;
public class MembershipDBOptions
{
    public const string SectionKey = "MembershipDBOptions";
    public string ConnectionString { get; set; } = string.Empty;
}
