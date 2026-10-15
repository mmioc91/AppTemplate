using Microsoft.AspNetCore.Identity;

namespace AppTemplate.Infrastructure.Persistence.Seeding;

// The roles every environment starts with: reference data with fixed, well-known ids, inserted if missing after every
// migration (RoleSeeder, ADR 0031). The template ships none — one row here per constant in Roles, each with a UUIDv7
// of its own, e.g. (Guid.Parse("01a1231c-0000-7000-8000-000000000101"), Roles.Admin).
internal static class RoleSeed
{
    private static readonly IReadOnlyList<(Guid Id, string Name)> Rows = [];

    public static IReadOnlyList<IdentityRole<Guid>> All() => Rows.Select(row => Role(row.Id, row.Name)).ToList();

    // NormalizedName is what Identity looks roles up by; the lookup normalizer upper-cases, so the seed does too.
    private static IdentityRole<Guid> Role(Guid id, string name) =>
        new(name) { Id = id, NormalizedName = name.ToUpperInvariant() };
}
