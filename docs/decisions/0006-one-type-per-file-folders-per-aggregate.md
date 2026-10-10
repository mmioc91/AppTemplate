# 0006. One type per file, folders per aggregate

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
Small types (IDs, enums, error-code classes) could be grouped into one file, and Domain could be organized by
kind of type (`Enums/`, `ValueObjects/`, `Entities/`) or by aggregate.

## Decision
- One type per file, file named after the type. A generic and a non-generic type with the same name get
  `Result.cs` / `ResultOfT.cs`.
- Domain folders per aggregate: `Orders/`, `Customers/`, … Everything that belongs to an aggregate — root, child
  entities, value objects, IDs, enums, `<Aggregate>Errors` — sits in its folder. Application and the test projects
  mirror those folders.

## Consequences
- Finding a type by name finds its file; git history is per type.
- Opening `Orders/` shows everything about orders in one place.
- Many small files.

## Alternatives considered
- **Folders by kind (`Enums/`, `ValueObjects/`)** — rejected: scatters one aggregate across many folders.
- **Grouping small types in one file** — rejected: breaks find-by-type-name.
