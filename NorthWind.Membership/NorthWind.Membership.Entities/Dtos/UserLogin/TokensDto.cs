namespace NorthWind.Membership.Entities.Dtos.UserLogin;
public class TokensDto(string accessToken)
{
    public string AccessToken => accessToken;
}
