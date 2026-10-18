using NorthWind.Entities.Interfaces;

namespace NorthWind.UserServices;

internal class UserServiceFake : IUserService
{
    public bool IsAuthenticated => true;
    public string UserName => "user@northwind.com";
    public string FullName => "Usuario de prueba";
}
