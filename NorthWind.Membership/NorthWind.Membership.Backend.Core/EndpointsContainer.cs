using NorthWind.Membership.Backend.Core.Controllers.UserRegistration;
namespace Microsoft.AspNetCore.Builder;
public static class EndpointsContainer
{
    public static WebApplication UseMembershipEndpoints(this WebApplication app)
    {
        app.MapPost(NorthWind.Membership.Entities.Endpoints.Register, UserRegistrationController.Register)
            .Produces(StatusCodes.Status200OK).Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json");
        app.UseUserLoginController();
        return app;
    }
}
