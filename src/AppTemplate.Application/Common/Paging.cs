namespace AppTemplate.Application.Common;

// Defaults and limits for paged queries (ADR 0022): a client that sends no page gets the first one, and no client can
// ask for more rows than MaxPageSize in one call.
public static class Paging
{
    public const int FirstPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
