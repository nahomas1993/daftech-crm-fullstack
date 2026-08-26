# DAFTECH CRM — Backend API

Clean Architecture ASP.NET Core 8 Web API backed by PostgreSQL, providing
authentication, ticket management, client/agreement tracking, reporting,
and file storage for the DAFTECH CRM system.

```
DaftechCrm.Domain          Entities, enums — no external dependencies
DaftechCrm.Application     DTOs, service interfaces + implementations, business rules
DaftechCrm.Infrastructure  EF Core (Npgsql/PostgreSQL), JWT, file storage, email, AI client
DaftechCrm.Api             Controllers, Program.cs, authorization policies, middleware
```

Dependencies point inward only — `Domain` has zero references to anything
above it, and `Application` depends on interfaces it defines, not on
`Infrastructure` directly.

---

## Requirements

- .NET 8 SDK
- PostgreSQL 14+ (or the bundled Docker Compose setup — see
  [`../deploy/README.md`](../deploy/README.md))
- `dotnet-ef` CLI: `dotnet tool install --global dotnet-ef`

## Getting started (local development)

1. **Configure secrets.** Never put real credentials in `appsettings.json`
   — use user-secrets:

   ```bash
   cd src/DaftechCrm.Api
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:Postgres" \
     "Host=localhost;Port=5432;Database=daftech_crm;Username=postgres;Password=YOUR_PASSWORD;"
   dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
   ```

   SMTP is required for credential-delivery emails to actually send (the
   app still works without it — see *Account provisioning* below):

   ```bash
   dotnet user-secrets set "Smtp:Host" "smtp.yourprovider.com"
   dotnet user-secrets set "Smtp:Username" "your-smtp-username"
   dotnet user-secrets set "Smtp:Password" "your-smtp-password"
   ```

2. **Generate the database migration.** No migrations are checked in yet —
   run this once against your local PostgreSQL instance:

   ```bash
   dotnet ef migrations add InitialCreate --project ../DaftechCrm.Infrastructure --startup-project .
   ```

3. **Run it.** Pending migrations apply and baseline demo data seeds
   automatically on startup:

   ```bash
   dotnet run --project src/DaftechCrm.Api
   ```

   Swagger UI is available at `/swagger` in development, with a **Bearer
   token** auth box pre-configured — log in via `/api/auth/employee-login`
   or `/api/auth/client-login`, paste the returned `accessToken` into the
   Swagger "Authorize" dialog, and every subsequent request in the UI
   carries it automatically.

### Demo accounts

Every seeded account (`Infrastructure/Persistence/SeedData.cs`) shares one
password so you can log in immediately:

| Username | Name | Role | Password |
|---|---|---|---|
| `na1001` | Nahom Alehegne | Admin | `DaftechDemo1!` |
| `ns1002` | Nebil Sherefa | IT Support | `DaftechDemo1!` |
| `mf1003` | Mekdes Fikru | Employee/Technician | `DaftechDemo1!` |
| `rg1004` | Robel Getachew | Employee/Technician (Disabled) | `DaftechDemo1!` |
| `at2001` | Abyssinia Traders PLC (client) | — | `DaftechDemo1!` |
| `mm2002` | Merkato Micro-Finance (client) | — | `DaftechDemo1!` |

None of these require a forced password change — that flow only triggers
for accounts created through the real registration endpoints below, which
issue a random one-time password instead.

---

## Authentication & authorization

Every endpoint except login, refresh, and the change-password flow requires
a valid JWT. There is no anonymous access to business data.

- **Access tokens**: short-lived (15 min default), HMAC-SHA256 signed,
  carry the account's type (Employee/Client) and roles as claims.
- **Refresh tokens**: longer-lived (14 days default), stored server-side
  as a SHA-256 hash only — the raw value is never persisted. Each refresh
  rotates the token: the old one is revoked and a new one issued. Reusing
  an already-rotated token is treated as a stolen-token signal and revokes
  every other active session for that account.
- **Authorization policies**: `AdminOnly`, `AdminOrItSupport`, `AnyEmployee`,
  `AnyClient`, `AnyAuthenticated` — applied per-endpoint to match who
  actually calls it (see `Api/Auth/AuthorizationPolicies.cs`).
- **Rate limiting**: 100 requests/minute per IP globally; 10/minute on
  login and refresh endpoints specifically, to slow down credential
  stuffing.

The app refuses to start if `Jwt:SigningKey` is missing or shorter than
32 bytes — there's no insecure fallback.

## Account provisioning

There is no self-service signup for staff or clients. Every account is
created by an Admin — staff via `POST /api/employees`, clients via
`POST /api/clients/register` — and is `Approved`/active immediately.

Either path:

1. Generates a username from initials + random digits, retrying on
   collision (`AccountCredentialService`).
2. Generates a random one-time password, hashed (PBKDF2-SHA256) before
   storage — plaintext is never persisted.
3. Emails the plaintext credentials via SMTP, with automatic retry on
   transient failures (exponential backoff via Polly — see
   `MailKitEmailSender`).
4. Returns the plaintext credentials **once**, in the registration
   response, regardless of whether the email sent — so the Admin always
   has a fallback if delivery failed. A dedicated resend endpoint
   (`POST /api/employees/{id}/resend-credential-email`) generates a fresh
   one-time password if needed.
5. Sets `MustChangePassword = true`. The account gets **no access token**
   on first login until the password is actually changed — a leaked
   one-time password can't be used for anything except the
   change-password call itself.

## File storage

Agreement scanned documents are uploaded via
`POST /api/agreements/{id}/scanned-file` (multipart, Admin/IT Support
only) and served back via `GET /api/agreements/{id}/scanned-file`.
Storage is pluggable behind `IFileStorageService` — the only
implementation today is local-filesystem, organized under
`{root}/{yyyy}/{MM}/{guid}.{ext}`, with extension allow-listing, a size
cap, and path-traversal protection on every read/delete. In the Docker
deployment, this directory is a named volume so uploads survive container
recreation.

## Ticket lifecycle

```
Submitted → Forwarded → Assigned (auto) → In Progress → Resolved
  → AwaitingClientConfirmation → { Closed | Escalated | Closed (auto) }
```

- Assignment is **fully automatic** — the moment IT Support forwards a
  ticket, `TicketAssignmentService` picks the Active technician with the
  fewest open tickets and assigns it in the same request. There is no
  manual "assign" endpoint.
- Marking a ticket **Resolved** doesn't close it — it starts a client
  confirmation window (default 5 days).
- The client responds via `POST /api/tickets/{id}/confirm`: a "No" reopens
  the ticket to the assignee with no rating recorded. A "Yes" requires a
  1–5 star rating, converted to a 0–100 score (`stars * 20`): **≥ 90**
  closes the ticket; **below 90** escalates it to `GET /api/tickets/escalated`
  for Admin review.
- Unanswered confirmations auto-close after the deadline
  (`AutoCloseTicketsHostedService`, polling every 15 minutes) — tagged
  distinctly and excluded from satisfaction averages.

Both the score threshold and the confirmation window are configurable in
`appsettings.json` under `TicketWorkflow`.

## Session & presence tracking

Every login opens a `LoginSession`. The frontend heartbeats
`POST /api/sessions/touch` roughly once a minute while active; identity is
read from the caller's own JWT, not a client-supplied ID, so one
authenticated user can't touch or close another account's session.
`SessionSweepHostedService` (every 2 minutes) flips any session whose last
heartbeat is older than `Session:OfflineAfterMinutes` (default 5) back to
offline. `GET /api/sessions/activity` is the Admin's live presence view.

## Health checks

- `GET /health/live` — no dependency checks; confirms the process is
  responding. Used by orchestrators to decide whether to restart.
- `GET /health/ready` — database + file storage; used to decide whether
  to route traffic to this instance.
- `GET /health` — everything including SMTP reachability, which reports
  `Degraded` rather than failing the whole response (email is a soft
  dependency).

All three return structured JSON with per-check status and timing.

---

## Testing

```bash
dotnet test tests/DaftechCrm.Tests/DaftechCrm.Tests.csproj
```

Covers the two highest-risk logic areas: JWT refresh-token rotation and
reuse/theft detection (`Auth/TokenServiceTests.cs`), and the ticket
satisfaction scoring/escalation threshold
(`Domain/TicketScoringTests.cs`). Runs against EF Core's InMemory
provider — no live database required.

## CI/CD

`.github/workflows/ci.yml` runs on every push/PR to `main`: restore,
build, test, then a Docker image build (not pushed) to verify the
container builds cleanly.

## Deployment

See [`../deploy/README.md`](../deploy/README.md) for the full Docker
Compose setup (PostgreSQL + API + frontend, reverse-proxied through
nginx), environment variable reference, and backup/restore scripts.

## Talking to the Angular frontend

See the sibling `daftech-crm` (Angular) project. Every service call goes
through a JWT-authenticated `HttpClient` with automatic token attachment
and silent refresh-on-401 — nothing runs on mock data.
