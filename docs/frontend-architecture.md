# Frontend Architecture

Angular 18 SPA (module-based, not standalone). Bun runtime + package manager. Bootstrap 5 + FontAwesome + ngx-toastr globally.

## Layout (`src/frontend/`)

```
src/
├── app/
│   ├── app.module.ts              # root module
│   ├── app-routing.module.ts      # top-level routes (lazy feature loads)
│   ├── app.component.ts/html/scss
│   ├── core/                      # singletons, loaded once
│   ├── shared/                    # cross-feature UI + utils
│   └── features/                  # lazy-loaded feature modules
├── environments/                  # environment.ts + environment.prod.ts
├── assets/
├── styles.scss
└── index.html
```

## Core (`app/core/`)

Singletons. Import `CoreModule` only in `AppModule`.

- **services/** — `AuthService`, `GoogleAuthService`, `ProfileService`, `HoldingService`, `TransactionService`, `InstrumentService`, `SIPPlanService`, `HomeService`, `LoadingService`. Talk to backend API over HTTP.
- **guards/** — `AuthGuard` protects feature routes.
- **interceptors/** — `JwtInterceptor` (attach access token, refresh on 401), `ErrorInterceptor` (global error handling).
- **models/** — `auth.model.ts`, `portfolio.model.ts`. Shared TS types.

## Shared (`app/shared/`)

Cross-feature components + pipes + directives, exported via `SharedModule`.

## Features (`app/features/`)

Each feature is its own module w/ routing, lazy-loaded.

| Feature | Route | Pages |
|---|---|---|
| `landing` | `/` | landing page |
| `auth` | `/auth` | login, signup, forgot-password, reset-password |
| `home` | `/home` (default protected) | dashboard, holdings, transactions, instruments, sip-plans, profiles, home |

`auth/` exposes `auth-form.utils.ts` + `_auth-shared.scss` for reuse across auth pages.

`home/` pages live under `home/pages/` and share styling via `shared-page.scss`.

## Routing

Top-level routes declared in `app-routing.module.ts`. Features loaded via `loadChildren` — keeps initial bundle small.

`AuthGuard` gates `/home/*`. Unauthenticated → redirect `/auth/login`.

## Auth Flow

```
Login form
  → AuthService.login() → POST /api/auth/login
  ← access token (in-memory) + refresh token (HttpOnly cookie, set by API)
  → BehaviorSubject updates auth state
  → JwtInterceptor attaches Authorization: Bearer on subsequent reqs
  → On 401: AuthService.refresh() → retry original req
  → Logout: clears state + calls /api/auth/logout
```

Access token lives in memory (lost on reload — acceptable since refresh cookie rehydrates). Refresh token never readable from JS (`HttpOnly`).

## Environments

- `environments/environment.ts` — dev, `apiUrl` → `http://localhost:5274`.
- `environments/environment.prod.ts` — prod, generated in CI from `FRONTEND_ENV` secret. Holds `apiUrl` + OAuth client IDs.

Swap via Angular `fileReplacements` in `angular.json`.

## Global Assets

`angular.json` `styles`/`scripts`:
- Bootstrap 5 CSS + JS bundle
- FontAwesome Free
- App `styles.scss`

No per-component import needed.

## Build + Test

```bash
bun install                      # or npm install
bun start                        # dev :4200
bun run prod                     # ng build --configuration production → dist/portivio
bun run test                     # Karma + Jasmine
bun run lint                     # ng lint

ng test --include='**/auth.service.spec.ts'    # single spec
```

Tests: Karma runner + Jasmine framework, Chrome headless in CI.

## CI

`.github/workflows/frontend.yml`:
1. Checkout
2. Write `FRONTEND_ENV` secret → `environment.prod.ts`
3. `bun install --frozen-lockfile` — lockfile MUST be committed in sync w/ `package.json`
4. `bun run test` — headless Chrome, no watch
5. `bun run prod` — production build
6. Build + push Docker image to Docker Hub (`rghvgrv/portifio-ui:latest` + sha tag)
7. Prune old tags (keep 3)

## Conventions

- Module-based (not standalone components).
- Services singleton via `providedIn: 'root'` or `CoreModule`.
- DTOs mirror backend shapes in `Portivio.Application/DTOs/` — keep in sync.
- New API call → add method in matching `core/services/*.service.ts`.
