namespace AppTemplate.Application.Auth;

// The roles a user can hold. The template ships none: add a constant per role here and its row to RoleSeed in
// Infrastructure (reference data with a well-known id, seeded after every migration), then build the Api's
// authorization policies on them. API contract: never rename a value.
public static class Roles
{
    public static readonly IReadOnlyList<string> All = [];
}
