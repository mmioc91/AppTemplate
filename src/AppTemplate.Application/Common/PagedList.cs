namespace AppTemplate.Application.Common;

// One page of a list that can grow (ADR 0022): the rows plus what a pager needs. Page is 1-based; Total counts every
// row, not just this page's.
public sealed record PagedList<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
