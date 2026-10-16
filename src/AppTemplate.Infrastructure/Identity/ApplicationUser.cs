using Microsoft.AspNetCore.Identity;

namespace AppTemplate.Infrastructure.Identity;

// The Identity user (ADR 0031). Not a domain concept: aggregates refer to a user by its Guid only. UserName is the
// email; the id is a UUIDv7 set by the use case, never by the database.
internal sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}
