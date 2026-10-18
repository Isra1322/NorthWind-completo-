using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NorthWind.Membership.Backend.Core.Options;
namespace NorthWind.Membership.Backend.Core.Services;
internal class JwtService(IOptions<JwtOptions> options)
{
    public string GetToken(UserDto userData)
    {
        var settings = options.Value;
        if (Encoding.UTF8.GetByteCount(settings.SecurityKey) < 32 ||
            string.IsNullOrWhiteSpace(settings.ValidIssuer) || string.IsNullOrWhiteSpace(settings.ValidAudience) ||
            settings.ExpireInMinutes <= 0)
            throw new InvalidOperationException("La configuracion JWT no es valida.");
        SigningCredentials credentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecurityKey)), SecurityAlgorithms.HmacSha256);
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, userData.Email),
                new Claim("FullName", $"{userData.FirstName} {userData.LastName}")
            }),
            Issuer = settings.ValidIssuer,
            Audience = settings.ValidAudience,
            Expires = DateTime.UtcNow.AddMinutes(settings.ExpireInMinutes),
            SigningCredentials = credentials
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
