using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NorthWind.Membership.Backend.AspNetIdentity;
using NorthWind.Membership.Backend.Core.Interfaces.Common;
namespace Microsoft.Extensions.DependencyInjection;
public static class DependencyContainer
{
    public static IServiceCollection AddMembershipIdentityServices(this IServiceCollection services,
        Action<MembershipDBOptions> configureMembershipDBOptions)
    {
        services.Configure(configureMembershipDBOptions);
        services.AddDbContext<MembershipContext>((provider, options) =>
            options.UseSqlServer(provider.GetRequiredService<IOptions<MembershipDBOptions>>().Value.ConnectionString));
        services.AddIdentityCore<MembershipUser>(options =>
        {
            options.Password.RequiredLength = 6;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
        }).AddUserValidator<RegistrationUserValidator>().AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>().AddEntityFrameworkStores<MembershipContext>();
        services.AddScoped<IMembershipService, MembershipService>();
        return services;
    }
}
