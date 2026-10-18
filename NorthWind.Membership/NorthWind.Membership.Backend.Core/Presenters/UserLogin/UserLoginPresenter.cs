using NorthWind.Membership.Backend.Core.Services;
using NorthWind.Membership.Backend.Core.Resources;
namespace NorthWind.Membership.Backend.Core.Presenters.UserLogin;
internal class UserLoginPresenter(JwtService service) : IUserLoginOutputPort
{
    public IResult Result { get; private set; } = Results.Ok();
    public Task Handle(Result<UserDto, IEnumerable<ValidationError>> userLoginResult)
    {
        userLoginResult.HandleResult(user => Result = Results.Ok(new TokensDto(service.GetToken(user))), errors =>
        {
            ProblemDetails details = new()
            {
                Status = 400, Title = UserLoginMessages.UserLoginErrorTitle,
                Detail = UserLoginMessages.UserLoginErrorDetail,
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
                Instance = "ProblemDetails/UserLoginPresenter"
            };
            details.Extensions["errors"] = errors.ToArray();
            Result = Results.Problem(details);
        });
        return Task.CompletedTask;
    }
}
