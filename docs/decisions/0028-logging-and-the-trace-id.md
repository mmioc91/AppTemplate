# 0028. Logs are the built-in logging, JSON lines in production, tied to errors by the W3C trace id

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
An open question asked for structured logging and a correlation id that ties a 4xx/5xx response to its log entries
([0018](0018-http-error-contract.md)). A user reporting "something went wrong" had nothing to point at, and a use
case that failed left no trace of its outcome. Both projects built from this template answered the id the same way
and the library differently: one kept the built-in logging with a JSON console (one dependency less), the other took
Serilog with Seq, a request-logging event and logging decorators around every use case.

## Decision
- **No logging library:** `Microsoft.Extensions.Logging` with `[LoggerMessage]`. In Production the console writes
  **JSON lines** with scopes and UTC timestamps (`appsettings.Production.json`), which container platforms collect
  and search; Development keeps the readable console.
- **The correlation id is the W3C trace id** of the request's `Activity`, not an id of our own. Every log entry of a
  request carries `TraceId` (and `SpanId`, `ParentId`) in its scope (`ActivityTrackingOptions`); every ProblemDetails
  carries the same value as `traceId` — the framework's default writes the whole `traceparent` form there
  (`00-<trace>-<span>-01`), which `ProblemDetailsSetup` replaces with the bare trace id so it equals the log scope's;
  outside a trace, the request's own identifier. A caller that sends `traceparent` (a proxy, a frontend trace)
  continues its own trace.
- **Every use case is logged by a decorator, outside validation** — Logging → Validation → handler, registered by
  `AddApplication` so no handler escapes it: the use case's name and duration at Debug on success; a failed `Result`
  at Information with its code and kind (`VALIDATION_FAILED` included); an exception at Information by type only (the
  exception itself is logged where it is handled). **The command or query is never logged**: it may hold personal
  data. Events carry ids only, never a name, address or link.
- **Levels:** a failed use case is Information, not Warning — a 409 or a refused input is the client's normal
  business, not something an operator must look at.
- A frontend shows the trace id (its first characters are enough) next to the message of a server fault, for the
  user to quote.

## Consequences
- A support request quoting a `traceId` finds the request's log entries, the use case's outcome included, with one
  search; a trace collector (OpenTelemetry) can be added later without changing the id.
- Every successful use case costs one Debug entry, every failed one an Information entry.
- Serilog and Seq remain a project's option (a Seq sink from a `Seq:ServerUrl` setting, one event per request through
  `UseSerilogRequestLogging`, `AddSerilog(preserveStaticLogger: true)` so the test hosts never share a static
  logger); the decorators and the id stay as they are.

## Alternatives considered
- **Serilog by default** — rejected for now: the built-in JSON formatter and scopes cover what is needed, with one
  dependency less, and the host's providers stay pluggable.
- **An `X-Correlation-Id` header of our own** — rejected: the W3C trace context is already there, propagated by
  ASP.NET Core and `HttpClient`, and understood by every tracing tool.
- **Logging the command on failure** — rejected: commands carry personal data; codes and ids say what failed.
