# Verification notes — Issue #67

Date: 2026-05-16

## Automated verification (local)

- Backend: `cd src/backend && dotnet build && dotnet test Portivio.Tests`
- Frontend: `cd src/frontend && bun run prod && bun run test`
- Android: `cd src/Android && npm run lint`

## Manual verification (development environment)

Not executed in this worker run (no local SMTP/Mailpit + running API/Hangfire instance available in this environment).

Suggested checklist when running the API locally in `Development`:

- Manual send:
  - Authenticate in the SPA.
  - Call `POST /api/email-summary/send-now`.
  - Confirm response indicates queueing, Hangfire job creation, and preference `lastSendStatus=Queued`.
  - Re-call during cooldown window and confirm `409 Conflict` (cooldown active).
- Template rendering:
  - Inspect the received email in Mailpit (or configured SMTP inbox) for both HTML + plain-text bodies.
- Scheduled dispatcher:
  - Configure a near-future schedule.
  - Confirm `nextRunAtUtc` is set and updates after enqueue.
  - Confirm lock/claim behavior (`lockedUntilUtc`) prevents double-send.
- Skip cases:
  - Inactive/unverified users: scheduled sends should record `Skipped` without sending.
  - Empty scheduled accounts: scheduled sends should record `Skipped` without sending.

