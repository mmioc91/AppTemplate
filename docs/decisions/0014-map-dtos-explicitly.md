# 0014. Map DTOs explicitly

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
Convention-based mappers (Mapster, AutoMapper) turn an aggregate into a DTO with one call. Aggregates expose value
objects, strongly-typed IDs and smart enums that need explicit conversion (`.Value`, `.Name`) anyway, and a renamed
property silently maps to an empty value.

## Decision
Build DTOs explicitly (constructor or object initializer) in the use case. Mapster is allowed only for large, flat,
one-to-one mappings where writing it by hand adds nothing — and never for types with value objects or smart enums
without an explicit configuration.

## Consequences
- A renamed or removed field breaks the build instead of silently producing an empty value.
- Slightly more code per response.

## Alternatives considered
- **Mapster everywhere** — rejected: convention-based mapping hides mistakes, and domain types need custom
  conversions anyway.
