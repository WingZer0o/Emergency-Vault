# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Run from `frontend/auth-client/`:

```bash
npm install
npm start              # ng serve, http://localhost:4200 — expects the API at localhost:8080 (see environment.development.ts)
npm run build           # ng build -> dist/auth-client
npm test                 # ng test, runs Vitest
```

To run a single test file/spec with Vitest, pass a path filter, e.g. `npx vitest run src/app/app.spec.ts`.

Prettier config is in `.prettierrc` (100-char width, single quotes, Angular parser for `.html`).

## Architecture

Angular 22, standalone components (no `NgModule`s) with signals for state, Angular Material for UI.

- **`app.config.ts`** — root DI setup. Registers the router, HTTP client with `authInterceptor` + `refreshInterceptor` (order matters — auth attaches the token, refresh handles 401s), and an `APP_INITIALIZER`-equivalent (`provideAppInitializer`) that calls `AuthService.tryRestoreSession()` before the app renders, so a valid refresh cookie silently re-establishes the session on page load/reload.
- **`core/`** — cross-cutting singletons:
  - `services/auth.service.ts` — the only source of auth state. Access token lives in a signal **in memory only** (never `localStorage`/`sessionStorage`) to limit XSS blast radius; the refresh token is an httpOnly cookie the service never reads directly. `currentUser`/`isAuthenticated` are derived signals — read these rather than duplicating auth state elsewhere.
  - `interceptors/auth.interceptor.ts` — attaches `Authorization: Bearer <token>` and `withCredentials: true`, but only for requests to `environment.apiUrl` (so it doesn't leak the token to third-party requests).
  - `interceptors/refresh.interceptor.ts` — on a 401 from any endpoint other than login/register/refresh themselves, calls `AuthService.refresh()` once and retries the original request; does not retry a second time on repeat failure.
  - `guards/auth.guard.ts` — redirects to `/login` if not authenticated. `guards/role.guard.ts` — a factory (`roleGuard(...roles)`) for role-gated routes; currently defined but not wired into `app.routes.ts` (no admin-only screen exists yet).
- **`features/`** — route-level components, one folder per screen (`auth/login`, `auth/register`, `auth/forgot-password`, `auth/reset-password`, `auth/confirm-email`, `dashboard`). All routes are lazy-loaded via `loadComponent` in `app.routes.ts`. Only `/profile` is guarded.
- **`environments/`** — `apiUrl` differs by build: `http://localhost:8080/api` in dev (cross-origin from `:4200`, requires the backend's `Cors:AllowedOrigins` to include the dev server origin), `/api` in production (nginx reverse-proxies same-origin, avoiding CORS/cross-site-cookie issues entirely).

This app is purely a client for the ASP.NET Core API in `../backend` — see `../backend/CLAUDE.md` and the root `README.md` for the auth/token design it depends on (ES512 access tokens, httpOnly refresh cookie scoped to `/api/auth`, email-code two-factor flow).
