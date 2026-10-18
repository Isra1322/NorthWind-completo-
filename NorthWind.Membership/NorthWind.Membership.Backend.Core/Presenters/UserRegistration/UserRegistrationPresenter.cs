using NorthWind.Membership.Backend.Core.Resources;
namespace NorthWind.Membership.Backend.Core.Presenters.UserRegistration;
internal class UserRegistrationPresenter : IUserRegistrationOutputPort
{
    public IResult Result { get; private set; } = Results.Ok();
    public Task Handle(Result<IEnumerable<ValidationError>> userRegistrationResult)
    {
        userRegistrationResult.HandleResult(errors =>
        {
            ProblemDetails details = new()
            {
                Status = 400,
                Title = UserRegistrationMessages.UserRegistrationErrorTitle,
                Detail = UserRegistrationMessages.UserRegistrationErrorDetail,
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
                Instance = "ProblemDetails/UserRegistration"
            };
            details.Extensions["errors"] = errors.ToArray();
            Result = Results.Problem(details);
        }, () => Result = Results.Ok());
        return Task.CompletedTask;
    }
}
