# 0015. Private parameterless constructors for EF Core

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
EF Core builds entities when it reads them. It can bind a constructor only if every parameter matches a mapped
scalar property by name. Domain constructors take things EF can't pass (a `now` timestamp, a value object) and Domain
must stay free of EF.

## Decision
An aggregate or child whose constructor EF can't bind gets a `private` parameterless constructor marked "for EF Core
materialization only". EF fills get-only properties through their backing fields (listed explicitly in the
configuration) and private collections through their fields. Types whose constructor EF can bind don't get one.

## Consequences
- Domain has no EF reference; the constructor is private, so neither Domain code nor Application can create an
  unvalidated aggregate.
- An EF-created aggregate skips its factory's validation — acceptable, because only rows written through the factory
  exist in the database.

## Alternatives considered
- **Rename constructor parameters so EF can bind them** — rejected: doesn't solve value-object parameters.
- **Separate persistence models mapped to aggregates** — rejected: a second model and a mapping layer for no gain.
