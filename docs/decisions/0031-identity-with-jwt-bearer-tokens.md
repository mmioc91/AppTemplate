# 0031. Authentication: ASP.NET Core Identity with JWT bearer tokens

- **Status:** Accepted
- **Date:** 2026-10-14

## Context
The template left the authentication scheme open (cookie or JWT). Two projects built from it chose differently and
both worked: one an httpOnly cookie for a same-origin SPA, one JWT bearer tokens for an SPA with a live hub and a
possible mobile client. Every later project has users, and the sign-in screen is usually its first screen, so the
template should ship one working scheme carrying the lessons of both, not a placeholder.

## Decision
**ASP.NET Core Identity for users, passwords and roles; JWT bearer tokens for the Api.** The cookie scheme stays
described under the alternatives, for a project that is a same-origin SPA and nothing else.

**Tokens.** A short-lived access token — an HMAC-SHA256 JWT of 15 minutes with the claims `sub`, `email`, `name`,
one `role` per role and `jti` — that the client keeps in memory, and a refresh token — 256 random bits as base64url,
14 days — that it stores and trades for a new pair. Both are issued and validated on the host's `TimeProvider` (the
Api installs a `LifetimeValidator`, because the token library reads the system clock), with 30 s of clock skew and
inbound claim mapping off, so the claim names stay as issued. Issuer, audience and lifetimes are `JwtOptions`, bound
in `Program.cs` with `ValidateOnStart`; the signing key is base64 of at least 32 bytes and comes from user-secrets or
the environment, never from a committed file.

**Refresh tokens are stored hashed and rotated.** The table `refresh_tokens` holds the SHA-256 hash (unique), the
user (a foreign key with `ON DELETE CASCADE` — the token is the user's, not a cross-aggregate reference), a family
id, the created/expires/revoked instants and the replacement's id. Every refresh revokes the presented token and
issues a new one of the same family. A spent token presented again means two parties hold it: the whole family is
revoked and the request fails — except within `TokenService.ReuseGrace` (30 s) of its rotation while its replacement
is still unused, which is the same client whose answer was lost (a page reloaded mid-refresh); then the unused
replacement is retired and a new one issued in its place. Two rotations of one token at the same instant are caught
by `xmin`: the loser reads the family again and is then decided by the reuse rules, exactly like a token presented a
moment later (the grace for the same client, a dead family for a copy); a family revoke that loses to a concurrent
writer reloads and goes again, so the replacement dies too; a refresh-token insert that hits its own row on a retried
commit is a success ([0013](0013-save-outcomes-unique-violations-and-conflicts.md)). Rows past their own expiry are
deleted — the user's at every sign-in, the family's at every rotation — with one statement over the user or family
index: a spent token still in date is kept, so a copy of it is still caught, and an expired one could only be told
from an unknown token, which answers the same 401. Logout revokes the family of the presented token,
only when it belongs to the signed-in user, and always answers 204 — an unknown token is a logout that already
happened. Access tokens are not revocable; that is why they are short.

**One database, one context.** `AppDbContext` derives from `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`:
one migration history, one seed path, one transaction when a user and its data are written together. Identity's
tables are renamed to the snake_case the rest of the schema uses (`users`, `roles`, `user_roles`, …) and its three
explicitly named indexes get names spelled as the naming convention would — the only hand-written names in the
model; the e-mail index is unique in the database, not only checked in code, so concurrent registrations of one
address cannot both succeed. User ids are UUIDv7 created by the use case ([0003](0003-strongly-typed-ids.md)).
Identity is configured as "core" only — no cookies, no `SignInManager`: the password rule is a minimum length of 10
and nothing else (length beats character classes); the email is unique and also the user name, with Identity's
user-name alphabet off (it refuses valid addresses such as `o'brien@example.com`; the validators check the address);
lockout after 5 failed attempts for 15 minutes, on from the first attempt. Identity writes the user row with its
concurrency stamp and reports a lost race without throwing: a failed-attempt count or its reset that lost is made
again on the fresh row, so guesses sent together all count and a second submit of the right password still signs in.
The design-time factory builds the model with the same Identity options, so a migration is made from the model the
Api runs.

**Roles** are reference data with well-known ids, inserted if missing after every migration through EF's seeding
hook (`UseSeeding`/`UseAsyncSeeding` in `DependencyInjection.Configure`, so `dotnet ef database update` seeds too),
straight through the `DbContext` so no Identity services are needed where migrations run — and only on a database at
the latest migration, so reverting one (`database update 0`, `migrations remove`) never seeds a table that is gone. The template ships none: a
project adds its constants to `Roles`, its rows to `RoleSeed`, and builds its authorization policies on them
(`RequireRole`) in `AuthenticationSetup`.

**Two ports, implemented in Infrastructure.** `IIdentityService` (create a user with an optional role, find by id,
check a password honoring lockout, roles) on `UserManager`, and `ITokenService` (issue an access token, create /
rotate / revoke refresh tokens). Both persist their own writes: Identity's `UserManager` does so anyway, and a
refresh-token reuse must be recorded even though the use case fails — so the auth use cases are the one place that
does not go through `IUnitOfWork`. Creating a user writes the user and its role row in one transaction inside the
retrying strategy; a unique violation on the way out is decided by the database (0013). `ICurrentUser` is
implemented in the Api from the principal's `sub` claim.

**Nothing a refusal says or takes tells an attacker which accounts exist.** `AUTH_INVALID_CREDENTIALS` (401) covers
an unknown email, a wrong password and a locked-out account alike, and every refusal costs one password hash:
`CheckPasswordAsync(email, password)` verifies against a dummy hash made by the configured hasher when there is no
real one to check (unknown email, lockout), so the answer takes as long either way. The port owns this, not the
handler, so no use case can skip it. Input longer than `AuthRules` allows is refused by the validator (400) before
anything is normalized, looked up or hashed, and so is an email with white space or a control character, or a
display name with a control character (Postgres refuses a NUL in text with an error the client would see as a 500).
Register, login and refresh are rate-limited per client — 10 a minute for register and login, 60 for refresh,
because every start of a web app spends one and an office shares an IP — and answer 429 `TOO_MANY_REQUESTS` with
`Retry-After`. A client is its IPv4 address (also when a dual-stack socket reports it IPv6-mapped) or its IPv6 /64,
since one host is usually given a whole /64 and could take a fresh address per request
([0026](0026-unauthorized-kind-and-too-many-requests-code.md)); the limiter runs after authentication, so a later
policy can count per account. `AUTH_EMAIL_TAKEN` (409) on register still reveals existence — accepted until email
confirmation exists.

**Pipeline and contract.** A fallback policy requires a signed-in user on every endpoint unless it is marked
`.AllowAnonymous()` (health, OpenAPI, register/login/refresh); `UseNotFoundForUnmatchedRoutes` before authorization
keeps unknown routes at 404 ([0018](0018-http-error-contract.md), [0029](0029-hosting-behind-a-proxy-with-migrations-as-a-deploy-step.md))
and lets routing's own rejections answer there too — a wrong method is 405, a wrong content type 415, signed in or
not, instead of a 401 that would send a client to sign in for a bug in its request.
Register answers **201** with the token pair (no `Location`: there is no resource for one user, `/me` is the signed-in
one). Identity's own refusals on register — an email it won't accept, a password under its minimum — become
validation errors on that field (`INVALID`, `TOO_SHORT`), so the client handles them like the validator's.
`AUTH_INVALID_REFRESH_TOKEN` (401) covers an unknown, expired, revoked or reused refresh token;
`AUTH_USER_NOT_FOUND` (404) is `/me` for a valid token that outlived its user.

## Consequences
- Every later use case gets its user from `ICurrentUser` and protects its endpoints by doing nothing — only an
  explicit `.AllowAnonymous()` opens one. A host test walks the endpoint table and fails if an unmarked endpoint lets
  an anonymous call through.
- Works the same for an SPA, a mobile app and a live hub on another origin (a hub takes the access token through the
  `access_token` query parameter; wired when the first hub needs a signed-in user).
- We own refresh-token storage and rotation: an Infrastructure table and the integration tests that pin it.
- A reload during a refresh keeps the session. A thief replaying a stolen token within those 30 s, before the
  rightful client uses its new token, gets a session; the rightful client's next refresh then meets a used
  replacement and the family dies — theft is still detected, a refresh later (one more, if that refresh itself
  lands inside the 30 s and meets an unused replacement).
- A lost acknowledgement on a refresh (retried commit) makes the client sign in again. Accepted as rare (0013).
- Lockout alone lets anyone lock a known account with five wrong passwords every fifteen minutes; the rate limit
  bounds how often. Brute force is the bigger risk, so lockout stays. Lockout runs on the system clock (Identity has
  no `TimeProvider`), and so does the rate limiter: a test can't fast-forward either.
- A family keeps sliding while its client refreshes; there is no absolute session lifetime. A project that needs one
  ("sign in again every 90 days") carries the family's start in each token and refuses rotation past it.
- `/me` reads the account from the database, so a renamed user or a changed role shows at once, while the access
  token keeps its claims until it expires.
- Identity's claim, login and token tables exist unused; dropping them would mean a custom user store for no gain.
- Deleting a user cascades to its refresh tokens and to Identity's own child rows (`user_roles`, `user_claims`,
  `user_logins`, `user_tokens`); nothing of ours cascades — a foreign key from an aggregate to a user stays
  `ON DELETE RESTRICT`, and account deletion is an explicit flow (open question "Account deletion").
- Deferred, as features of an account screen: email confirmation, password reset, "sign out everywhere".

## Alternatives considered
- **Identity cookie** (`__Host-`, `Secure`, `SameSite=Lax`, 14 days sliding; JSON-only bodies as the CSRF
  protection; nothing in browser storage) — simpler, and safer against XSS, for a same-origin SPA; a project that is
  only that may switch: `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`,
  `ConfigureApplicationCookie` with `OnRedirectToLogin`/`OnRedirectToAccessDenied` answering 401/403 instead of
  redirecting, a `SignInManager` whose every refusal still costs a hash, and Data Protection keys persisted to the
  database (`PersistKeysToDbContext<AppDbContext>` with a fixed application name) so restarts and several instances
  keep users signed in — those keys are then as sensitive as the password hashes and need encrypting at rest before
  production. Rejected as the default: awkward for a mobile client, a hub on another origin or a third-party API
  client.
- **Identity's bearer endpoints (`MapIdentityApi`)** — opaque tokens with less control over claims and rotation.
- **A separate Identity `DbContext`** — two migration histories, two seed paths, and no transaction across a user and
  its first data.
- **`SignInManager.CheckPasswordSignInAsync`** — pulls in `IHttpContextAccessor` and the cookie schemes for a few
  lines of lockout logic that `UserManager` exposes directly.
- **Rotation logic in Application behind a refresh-token store port** — the rules are storage rules (hash lookup,
  family, `xmin`), and the writes must happen even when the use case fails; the integration tests pin the behavior.
- **Distinct codes for a wrong password and a lockout; measuring and padding the login time** — the codes tell an
  attacker which accounts exist and when a lockout lifts; padding is fragile, doing the same work is simpler and
  closer to constant.
- **`DefaultPolicy` + `RequireAuthorization()` per group** instead of the fallback policy — an endpoint mapped
  outside a group would be open by default.
- **The password minimum only in Identity** — a form would learn "too short" in a second round trip, after the other
  fields pass. The validator checks the same `AuthRules` constant; Identity stays the backstop, and its refusal still
  maps to the field.
- **One rate-limit window for register, login and refresh** — the users behind one IP would sign each other out.
