namespace Microsoft.AspNetCore.Builder;
internal static class UserLoginController
{
    public static WebApplication UseUserLoginController(this WebApplication app)
    {
        app.MapPost(NorthWind.Membership.Entities.Endpoints.Login,
            async (UserCredentialsDto userCredentials, IUserLoginInputPort inputPort, IUserLoginOutputPort presenter) =>
            {
                await inputPort.Handle(userCredentials);
                return presenter.Result;
            }).Produces<TokensDto>(200).Produces<ProblemDetails>(400, "application/problem+json");
        return app;
    }
}
