# 0027. Strict JSON, and an OpenAPI document fit for a generated client

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
[0009](0009-built-in-openapi.md) generates a typed client from `/openapi/v1.json`, so the document's types become the
frontend's types, and [0020](0020-missing-value-fields-are-required.md) made enums strict (names only). Both projects
built from this template found the same holes on their first screens:
- an instant (`DateTimeOffset`) without an offset — `"2026-06-14T09:00:00"`, or a bare date — is read in the
  **server's** time zone, so the same request means a different instant on a laptop and in a UTC container, with no
  error anywhere;
- the Web JSON defaults read numbers from strings, so every number in the document was `integer | string` with a
  digits pattern, and the generated client typed `total`, `page` and every count as `number | string`;
- a custom converter makes the generator write `format: date-time` without a `type`, which NSwag turns into `any`;
  an enum used nullable first lists `null` among its values and has no type, which NSwag turns into an empty
  interface;
- query parameters bound through records were named after the record's properties (`Page`, `Status`), not in
  camelCase, and a string parameter with a fixed set of values listed none.

## Decision
- **Instants need an offset:** `StrictInstantConverter` on the HTTP JSON options accepts an ISO 8601 date and time
  ending in `Z` or `±hh:mm` and nothing else, for `DateTimeOffset` and `DateTimeOffset?`; anything else is a
  `JsonException`, so 400 `MALFORMED_REQUEST` ([0018](0018-http-error-contract.md)). Writing is unchanged. Clients
  send instants with their offset (`toISOString()`). This covers JSON bodies only: a `DateTimeOffset` in a query
  string binds through `DateTimeOffset.TryParse`, in the server's time zone — bind such a parameter as a string and
  apply the same rule in the validator, or keep instants out of query strings.
- **Numbers are JSON numbers:** `JsonNumberHandling.Strict`; `"21"` is 400 and the schemas say `integer`. Query
  strings are unaffected (parsed, not JSON).
- **`ClientTypesTransformer`** (a schema transformer) gives an instant `type: string` (or `string | null`) next to its
  format, and an enum `type: string` without `null` among its values; nullability stays on the property.
- **Query parameters are named explicitly** in camelCase (`[FromQuery(Name = "page")]`), and a string parameter with
  fixed values lists them with **`[QueryValues(...)]`** (`QueryValuesTransformer`; documentation only, the validator
  still decides — [0022](0022-lists-that-can-grow-are-paged.md)). `AllowedValuesAttribute` would say the same, but the
  OpenAPI generator fails on it.
- **Pinned by tests that walk the whole document** (`OpenApiContract`, run on the live document by
  `OpenApiContractTests` and proved on fixtures by `OpenApiContractRulesTests`): no schema is both a number and a
  string, every formatted schema has a type, every enum is a string without `null`, every query parameter is
  camelCase. `ApiHostTests` pins the converter and the strict numbers; `ClientTypesTransformerTests` and
  `QueryValuesTransformerTests` the transformers.

## Consequences
- A client that sent numbers as strings or instants without an offset gets 400 instead of a silently wrong value; a
  generated client never does. `ProblemDetails.status` is a plain `integer` in the document too.
- A query parameter bound from an `[AsParameters]` record needs its `[FromQuery(Name = …)]` (a plain handler
  parameter is already camelCase), or the camelCase test fails.
- Smart enums travel as their names and the document types them as `string`; listing their values for the client is
  decided per type, when a screen switches on one.
- The document still says `date-time` for an instant; the stricter rule is this ADR and the converter.

## Alternatives considered
- **Read offset-less instants as UTC** — rejected: still silently wrong for a client that meant local time.
- **Read them in a configured time zone** — rejected: a converter can't see which record a request is about, and the
  client knows its own offset.
- **A transformer that drops the string form of numbers while still accepting `"21"`** — rejected: the document
  would describe a stricter API than the one running.
- **Bind a name-valued query parameter as an enum so the document lists the values** — rejected in 0022: an unknown
  value would fail binding instead of getting the validator's code on the field.
