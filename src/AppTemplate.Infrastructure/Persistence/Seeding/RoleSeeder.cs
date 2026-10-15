using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.Infrastructure.Persistence.Seeding;

// Insert-if-missing by id after every Migrate (DependencyInjection.Configure wires it as the seeding hook with
// RoleSeed.All()). Straight through the DbContext, not the RoleManager: the hook runs wherever migrations run,
// including `dotnet ef database update`, where no Identity services exist. Other reference data seeds follow the same
// shape.
internal static class RoleSeeder
{
    public static void Seed(DbContext db, IReadOnlyList<IdentityRole<Guid>> roles)
    {
        // The migration's context tracks nothing else; a retried migration must not meet the rows of the attempt that
        // was rolled back, tracked as if saved.
        db.ChangeTracker.Clear();
        var existing = db.Set<IdentityRole<Guid>>().Select(role => role.Id).ToList();
        db.Set<IdentityRole<Guid>>().AddRange(Missing(roles, existing));
        db.SaveChanges();
    }

    public static async Task SeedAsync(DbContext db, IReadOnlyList<IdentityRole<Guid>> roles, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        List<Guid> existing = await db.Set<IdentityRole<Guid>>().Select(role => role.Id).ToListAsync(cancellationToken);
        db.Set<IdentityRole<Guid>>().AddRange(Missing(roles, existing));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<IdentityRole<Guid>> Missing(IReadOnlyList<IdentityRole<Guid>> roles, List<Guid> existing) =>
        roles.Where(role => !existing.Contains(role.Id));
}
