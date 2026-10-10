# 0009. Built-in OpenAPI document

- **Status:** Accepted
- **Date:** 2026-09-29

## Context
The frontend's typed client is generated from an OpenAPI document. Swashbuckle or NSwag middleware can produce it,
but ASP.NET Core now ships its own generator.

## Decision
- The document comes from the built-in `Microsoft.AspNetCore.OpenApi`, one per API version via
  `Asp.Versioning.OpenApi` (`/openapi/v1.json`), mapped in Development only.
- A typed client (e.g. NSwag CLI) is generated from that document.
- An interactive UI (e.g. Scalar) is added only if we actually need one.

## Consequences
- No Swashbuckle/NSwag package in the Api project.
- Every endpoint must declare its result types, error statuses (`ProblemHttpResult` declares none) and `WithName`,
  or the generated client is poor; `ApiHostTests` pins each endpoint's status list.
- When CI needs the document, it is generated at build time (`Microsoft.Extensions.ApiDescription.Server`) rather
  than served in production.

## Alternatives considered
- **NSwag or Swashbuckle middleware** — rejected: an extra dependency for what the framework now does itself.
