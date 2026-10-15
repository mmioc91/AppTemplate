using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.Infrastructure.Persistence.Seeding;

// Insert-if-missing by id after every Migrate (DependencyInjection.Configure wires it as the seeding hook). Straight
// through the DbContext, not the RoleManager: the hook runs wherever migrations run, including `dotnet ef database
// update`, where no Identity services exist. Other reference data seeds follow the same shape.
internal static class RoleSeeder
{
    public static void Seed(DbContext db)
    {
        var existing = db.Set<IdentityRole<Guid>>().Select(role => role.Id).ToList();
        db.Set<IdentityRole<Guid>>().AddRange(Missing(existing));
        db.SaveChanges();
    }

    public static async Task SeedAsync(DbContext db, CancellationToken cancellationToken)
    {
        List<Guid> existing = await db.Set<IdentityRole<Guid>>().Select(role => role.Id).ToListAsync(cancellationToken);
        db.Set<IdentityRole<Guid>>().AddRange(Missing(existing));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<IdentityRole<Guid>> Missing(List<Guid> existing) =>
        RoleSeed.All().Where(role => !existing.Contains(role.Id));
}
