# DAFTECH CRM — Angular Frontend

Standalone-component Angular 17 application: a staff Admin/IT Support app
and a separate Client Portal, sharing one codebase and calling a real
ASP.NET Core + PostgreSQL backend over HTTP. See the sibling `DaftechCrm`
(.NET) project for the API, and `../deploy` for the containerized
deployment of both together.

---

## Running it locally

```bash
npm install
npm start        # ng serve, http://localhost:4200
```

Open `src/environments/environment.ts` and point `apiBaseUrl` at your
running API instance (e.g. `https://localhost:7001`). The backend's CORS
policy reads its allowed origins from configuration, so add
`http://localhost:4200` there if it isn't already present.

## Project layout

- `src/app/core/models.ts` — TypeScript types matching the API's DTOs
  field-for-field, including exact enum spelling (`ItSupport`,
  `InProgress`, etc., since these round-trip as JSON strings from C#
  enums). Human-readable labels live alongside as
  `TICKET_CATEGORY_LABELS` / `EMPLOYEE_ROLE_LABELS`.
- `src/app/core/services/` — one service per entity/concern, each backed
  by `HttpClient` and an Angular signal cache. Includes
  `token-storage.service.ts` (JWT persistence) and
  `pdf-export.service.ts` (client-side PDF generation).
- `src/app/core/interceptors/auth.interceptor.ts` — attaches the bearer
  token to every request and transparently refreshes on a 401.
- `src/app/staff/` — the Admin/Staff app.
- `src/app/portal/` — the Client Portal, including the **Confirm
  Resolution** flow (star rating on ticket closure).

---

## Authentication

Login (staff or client) returns a short-lived access token and a
longer-lived refresh token. Both persist in `localStorage` via
`TokenStorageService`, so a page refresh does not log the user out — an
`APP_INITIALIZER` restores the session (re-fetching the profile from the
decoded token's identity) before the app renders.

The `authInterceptor` attaches `Authorization: Bearer <token>` to every
API call automatically. On a `401`, it performs exactly one silent
refresh-and-retry, with in-flight de-duplication so several concurrent
requests hitting `401` at once share a single refresh call rather than
each triggering their own. If the refresh itself fails (expired or
revoked refresh token), the user is logged out cleanly.

Route guards (`core/guards/auth.guard.ts`) wait for session restoration to
finish before deciding whether to redirect, so a hard refresh never
flashes the login screen before the real auth state is known.

## Ticket workflow (client-facing)

Ticket assignment is fully automatic — there is no "Assign" control
anywhere in the UI; IT Support forwards a ticket and the backend assigns
it to whichever technician has the fewest open tickets in the same
request.

When an employee marks a ticket **Resolved**, it moves to *Awaiting
Client Confirmation*. The client sees it under the Portal's **Confirm
Resolution** page and rates it 1–5 stars:

- **≥ 90/100** (4–5 stars) → ticket closes normally.
- **Below 90** → escalates to the Admin's **Escalated — Needs Admin
  Review** queue.
- **No response** within the configured window → the backend auto-closes
  it with no rating recorded, so it can't skew satisfaction averages.

## File upload — agreement scans

The Agreements page performs a real multipart upload against the API's
file storage (`AgreementService.uploadScannedFile`) — an agreement is
created first, then the scanned document is attached as a second step
once an ID exists. Each row shows a working **Download** button that
fetches the file as a blob (so the auth header carries correctly) and
triggers a browser download.

## Reports & PDF export

Every report on the Reports page — including the on-time resolution
chart and the six operational reports (clients & agreements, tickets,
expiring agreements, maintenance history, time & performance,
satisfaction surveys) — generates a real PDF client-side via
`PdfExportService` (`jsPDF` + `jspdf-autotable`), built from data the app
already has loaded. No server-side rendering involved.

## Session & presence tracking

Logging in starts a heartbeat (`SessionService.startHeartbeat`, roughly
once a minute) that pings the backend to stay marked online and update
the last-seen timestamp; logging out stops it and closes the session.
Admins see live status for every account on the **Session Activity** page.
The backend derives *whose* session is being touched from the caller's
own JWT — the frontend no longer sends an account ID that could be
spoofed.

## AI-assisted performance reports

The **Employee Performance** page shows the same metrics either way, with
an optional "Add AI Summary" action for a narrative on top. If the
backend has no AI provider configured, it returns a plain "unavailable"
message instead of a paragraph — the underlying metrics are unaffected.

## Progressive Web App

Built with `@angular/service-worker`. The Admin/Staff app and Client
Portal each install as a separate app (distinct name, icon, start page)
via two manifest variants that `PwaManifestService` swaps based on route.
The service worker only activates in production builds — to test
installability locally, build and serve the `dist/` output with something
that respects the service worker (`ng serve` doesn't register it by
design):

```bash
npm run build -- --configuration production
npx http-server dist/daftech-crm/browser
```

---

## Testing & CI

```bash
npx tsc --noEmit -p tsconfig.app.json   # type-check
npm run build -- --configuration production
```

`.github/workflows/ci.yml` runs both on every push/PR to `main`, followed
by a Docker image build (not pushed) to verify the container builds
cleanly.

## Deployment

Built and served via `nginx` in production — see the multi-stage
`Dockerfile` and `nginx.conf` in this folder, and
[`../deploy/README.md`](../deploy/README.md) for the full Docker Compose
setup (PostgreSQL + API + this frontend). nginx reverse-proxies `/api/`
and `/health` to the backend container, so the browser only ever talks to
one origin and no cross-origin configuration is needed in that setup.
