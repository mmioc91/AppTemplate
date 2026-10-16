# 0034. Mail leaves through an outbox in the unit of work, claimed with a lease, sent outside any transaction

- **Status:** Accepted
- **Date:** 2026-10-16

## Context
Almost every product mails someone: a confirmation of an address, a reset of a password, an invitation, a notice. A
mail must not leave for a change that failed to save, must not make a request wait for a mail server, must survive a
mail server that is briefly down, and two instances of the Api must never send one mail twice. The template has no
mail flow of its own yet; the first one a project adds should find the plumbing in place, as it finds sign-in (ADR
0031).

## Decision
- **A use case queues mail explicitly** through `IMailQueue.Enqueue(OutgoingMail)`; the mail is a row in
  `queued_mails`, added to the request's `AppDbContext`, so the use case's own `SaveChangesAsync` commits it with the
  change — a transactional outbox. The side effect is written where it happens. Still no domain events: who receives
  what and how it reads is the use case's business, not a fact of the aggregate, and an event would only be translated
  back into the same mail; events wait for a side effect that several use cases trigger through one domain change.
- **Rendered first, from a template.** `IMailRenderer.Render(MailTemplate, values)` fills Handlebars placeholders —
  HTML-escaped in the body, taken as they are in the subject, `{{#if}}` for a part shown only with a value — and
  wraps the body in one layout with inline styles, in the app's colour and the language `Mail:Language` names. A
  project's templates are static `MailTemplate`s (`Application/Mail/MailTemplates`), each listing its placeholders;
  `MailRules` holds the lengths the outbox's columns allow. The links a mail carries come from `IWebLinks`: one method
  per page of the web app, built from `Web:BaseUrl`, a token in the query — where the page reads it and sends it in a
  request body — never in an API URL, which proxies and logs keep.
- **`MailDeliveryWorker`**, a hosted `BackgroundService` (one worker and one table; no job framework), drains the
  queue. A round first **claims** a batch (`Mail:BatchSize`, 20) in one statement —
  `UPDATE queued_mails SET next_attempt_at = now + lease WHERE id IN (SELECT … FOR UPDATE SKIP LOCKED) RETURNING *` —
  so two instances never take one mail and no lock outlives the statement; then it **sends** each mail through
  `IMailTransport` with no transaction open and saves its outcome at once, in a save of its own that the host's stop
  doesn't cancel. The lease (`MailDelivery.Lease`, 15 minutes) outlasts a batch's sending: a mail whose outcome wasn't
  saved (the instance died, the database was gone) falls due again when it ends. Delivery is **at least once**.
  Batches come straight on while they come full, then a pause (`Mail:PollSeconds`, 10).
- **A failed mail waits 1, 5, 15, 60, then 240 minutes**, and is given up after the sixth failure (`failed_at`). Only
  the exception's type is logged — an SMTP error can quote the address.
- **SMTP through MailKit** (`SmtpMailTransport`), the one protocol every provider speaks; the sender and the server
  come from `Mail:*` (validated on start: a password only with TLS), the password from secrets. Development sends to
  **Mailpit** (`docker compose up -d`, web UI on :8025), and so does CI: `ci-e2e.yml` runs Mailpit beside the Api, so
  a mail flow's end-to-end test can read the link back from Mailpit's API. The integration tests turn delivery off
  (`ApiFactory`) and read the queue; the transport itself is tested against a Mailpit container.
- **Sent and given-up rows are deleted after 30 days** by the worker (once an hour): they hold addresses. A bulk
  delete of Infrastructure's own rows, not of an aggregate.

## Consequences
- Every environment needs `Mail:FromAddress`, `Mail:SmtpHost` and `Web:BaseUrl`, or the Api refuses to start; the
  deploy stack takes them from its env file (`MAIL_*`, `WEB_BASE_URL`).
- A mail can't be "unsent" once its change is saved; a resend is a new row.
- A host stopped mid-batch delays that batch's unsent mail by up to the lease.
- One SMTP connection per mail: enough for an app's notifications; a provider's HTTP API or a pooled connection can
  replace the transport without touching the use cases.
- The outbox makes sending cheap, not free: an endpoint that mails someone, or tells whether an address has an
  account, gets a rate-limit policy of its own (ADR 0031).

## Alternatives considered
- **Sending in the handler after `SaveChanges`** — rejected: a request would wait on the mail server, and a failure
  would lose the mail with no retry.
- **Domain events dispatched after the commit** — rejected for now, see above; they also still need an outbox to be
  reliable.
- **Sending inside the claiming transaction** — rejected: the row locks would be held across SMTP, and the retrying
  execution strategy (ADR 0013) re-runs a block whose commit failed transiently — the batch would go out twice. A
  `locked_until` column instead of reusing `next_attempt_at` — rejected: "not before" already means that.
- **Hangfire** — rejected for now: its own schema and dashboard for what one worker and one table do.
