using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace NorthWind.Membership.Backend.AspNetIdentity;
internal class MembershipContextFactory : IDesignTimeDbContextFactory<MembershipContext>
{
    public MembershipContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<MembershipContext>()
        .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=NorthWindUsersDB").Options);
}
