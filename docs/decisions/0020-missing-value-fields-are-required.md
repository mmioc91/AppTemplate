# 0020. Missing value-type fields are REQUIRED, not defaults

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
JSON binding fills a property that's absent from the request with its type's default. For a reference type that's
`null`, which validation reports as `REQUIRED`. For a value type it's a real value: a missing enum binds to its first
member, a missing number to `0`, a missing bool to `false` — and passes validation silently.

## Decision
- **Value-type fields (enums, numbers, booleans, dates) are nullable in requests and commands** whenever the field
  is mandatory, and their validator rule starts with `NotNull().WithErrorCode(ValidationCodes.Required)`
  (`Cascade(CascadeMode.Stop)`). The handler reads the value after validation.
- **Enums bind by name only** (`JsonStringEnumConverter(allowIntegerValues: false)` in `Program.cs`). A value that
  isn't one of the names — a number, or an unknown name — can't be read at all, so the request is `400
  MALFORMED_REQUEST` without a field key. Accepted: a generated client sends valid names, so an unknown one is a
  client bug.
- The validator's `IsInEnum` stays: it guards callers that don't come through HTTP binding (jobs, other use cases).

## Consequences
- A missing field is always reported per field, the way a form expects ([0012](0012-validation-errors-carry-codes.md)).
- The OpenAPI document shows such fields as nullable; the frontend still marks them required.

## Alternatives considered
- **`[JsonRequired]` / required constructor parameters** — rejected: a missing field then fails binding (400
  `MALFORMED_REQUEST` with no field), which a form can't attach to a field.
- **Bind enums as strings and validate the name** — rejected: the OpenAPI document would lose the enum.
- **Reserve `0` as an `Unknown` enum member** — rejected: pollutes the domain enum with a value that must never be
  stored.
