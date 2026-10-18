namespace NorthWind.Membership.Backend.Core.Controllers.UserRegistration;
internal static class UserRegistrationController
{
    internal static async Task<IResult> Register(UserRegistrationDto userData,
        IUserRegistrationInputPort inputPort, IUserRegistrationOutputPort presenter)
    {
        await inputPort.Handle(userData);
        return presenter.Result;
    }
}
