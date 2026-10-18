using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NorthWind.Membership.Entities.Dtos.UserRegistration;

namespace NorthWind.Membership.Backend.AspNetIdentity;

public class MembershipContext(DbContextOptions<MembershipContext> options) : IdentityDbContext<MembershipUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        var user = builder.Entity<MembershipUser>();
        user.Property(u => u.FirstName).HasMaxLength(RegistrationLimits.FirstName).IsRequired();
        user.Property(u => u.LastName).HasMaxLength(RegistrationLimits.LastName).IsRequired();
        user.Property(u => u.Email).HasMaxLength(RegistrationLimits.Email);
        user.Property(u => u.NormalizedEmail).HasMaxLength(RegistrationLimits.Email);
    }
}
