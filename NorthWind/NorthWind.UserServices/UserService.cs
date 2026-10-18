using Microsoft.AspNetCore.Http;
using NorthWind.Entities.Interfaces;

namespace NorthWind.UserServices;

internal class UserService(IHttpContextAccessor contextAccessor) : IUserService
{
    public bool IsAuthenticated =>
        contextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
    public string UserName =>
        contextAccessor.HttpContext?.User.Identity?.Name ?? string.Empty;
    public string FullName =>
        contextAccessor.HttpContext?.User.Claims
            .FirstOrDefault(c => c.Type == "FullName")?.Value ?? string.Empty;
}
