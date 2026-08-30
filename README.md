# EmergencyVault

A full-stack authentication scaffold: ASP.NET Core Identity on .NET 10, Angular 22 + Angular
Material, EF Core against SQL Server with a repository pattern, and JWTs signed with ECDSA
P-521 (ES512). Refresh tokens are stored in SQL Server (source of truth) with a Redis cache for
fast validation.

## Stack

| Layer          | Tech                                                                 |
|----------------|-----------------------------------------------------------------------|
| Frontend       | Angular 22 (standalone components, signals), Angular Material          |
| API            | ASP.NET Core 10, minimal controllers delegating to services            |
| Auth           | ASP.NET Core Identity, JWT access tokens (ES512), refresh-token cookie |
| Database       | SQL Server (EF Core, code-first migrations)                            |
| Cache          | Redis (refresh-token fast-path validation)                             |
| Containers     | Docker Compose (SQL Server, Redis, API, frontend)                      |
| Desktop        | `deno desktop` (WebView2/WKWebView/WebKitGTK), signed with Azure Artifact Signing, packaged with Inno Setup (Windows) |

## Project layout

```
backend/
  EmergencyVault.slnx
  src/
    EmergencyVault.Domain          - entities (ApplicationUser, ApplicationRole, RefreshToken)
    EmergencyVault.Application     - DTOs, interfaces, services (business logic)
    EmergencyVault.Infrastructure  - EF Core DbContext, migrations, repositories, Redis, email
    EmergencyVault.Api             - controllers, Program.cs, Dockerfile
frontend/
  auth-client/                 - Angular app (core services/guards/interceptors, feature screens)
    server.ts                  - Deno server; also the desktop app's runtime shell (see below)
    deno.json                  - desktop build/installer tasks
    installer/                 - Inno Setup script (EmergencyVault.iss) + icon
docker-compose.yml             - base service definitions (sqlserver, redis, api, frontend)
docker-compose.override.yml    - auto-merged dev overrides: hot reload for api + frontend
docker-compose.prod.yml        - optimized build overlay (opt-in, see below)
```

## Running it

### Option 1: Docker, with hot reload (default)

```bash
cp .env.example .env    # first time only
docker compose up --build
```

`docker compose up` automatically merges `docker-compose.override.yml` on top of the base file.
That override runs `dotnet watch` for the API and `ng serve` for the frontend, with the source
bind-mounted from the host - edit a file and the running container picks it up:

- API: http://localhost:8080/swagger
- Frontend: http://localhost:4200

No extra flags needed; this is what `docker compose up` does by default.

### Option 2: Docker, production-like build

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build
```

This bypasses the override file, builds the optimized multi-stage image for both services
(compiled API, `ng build` served by nginx), and publishes the frontend on port 80 with nginx
reverse-proxying `/api/*` to the API container (same-origin, no CORS/cookie complications).

### Option 3: Visual Studio / CLI dev loop (no frontend/API containers)

Run just the infra in Docker, then debug natively:

```bash
docker compose up sqlserver redis -d
```

- Open `backend/EmergencyVault.slnx` in Visual Studio and F5 the `EmergencyVault.Api` project
  (`appsettings.Development.json` points at the Docker-exposed `localhost` ports).
- In `frontend/auth-client`, run `npm install` then `npm start` for the Angular dev server.

## Configuration

| Setting | Where | Notes |
|---|---|---|
| `SA_PASSWORD` | `.env` (gitignored, copy from `.env.example`) | SQL Server sa password |
| `JWT_SIGNING_KEY_PEM` | Infisical secret, injected as an environment variable | ECDSA P-521 PEM key used to sign JWTs |
| `Frontend:BaseUrl` | appsettings / `Frontend__BaseUrl` | Used to build email confirmation / password reset links, and the Stripe Checkout success/cancel redirect |
| `Cors:AllowedOrigins` | appsettings / `Cors__AllowedOrigins__0`, `__1`, ... | Must include whatever origin the browser loads the frontend from |
| `STRIPE_SECRET_KEY` / `STRIPE_PUBLISHABLE_KEY` | Infisical secret, injected as environment variables | Stripe API keys for the yearly membership paywall (test-mode keys for local dev) |
| `STRIPE_WEBHOOK_SECRET` | Infisical secret, injected as an environment variable | Verifies the signature on `POST /api/billing/webhook`; see below for how to get one locally |

### Billing / Stripe locally

The yearly membership price is admin-configurable (`/admin/billing-settings`) - it isn't read from
config. Stripe can't reach `localhost` directly, so webhook events (subscription activation,
renewal, cancellation) need the [Stripe CLI](https://docs.stripe.com/stripe-cli) forwarding to the
API:

```bash
stripe listen --forward-to localhost:8080/api/billing/webhook
```

The CLI prints a `whsec_...` signing secret on startup - put that in Infisical's `dev` environment
as `STRIPE_WEBHOOK_SECRET`. In production, create the webhook endpoint in the Stripe Dashboard
against `<deployed-api-url>/api/billing/webhook` instead, and use the secret it generates.

Email confirmation and password reset links are logged to the API console instead of being
emailed (`EmergencyVault.Infrastructure.Email.ConsoleEmailSender`) - swap in a real SMTP/SendGrid
implementation of `IEmailSender` for production.

## Deployment

Push to `main` triggers `.github/workflows/deploy.yml`: the backend Container App deploys first,
then the frontend Static Web App (`needs: deploy_backend`), so the API is always live before the
SPA that calls it. Each job's real work is skipped when its own path (`backend/**` or
`frontend/auth-client/**`) didn't change, but the job still runs so the ordering holds either way.

Azure resources (resource group `Application`):

| Resource | Name | Purpose |
|---|---|---|
| Container Registry | `evbackendacr` | Backend image, pulled by the Container App via its own managed identity (no admin credentials) |
| Container Apps environment | `ev-cae` | Hosts the backend |
| Container App | `ev-backend` | Runs the API; system-assigned managed identity |
| SQL Server | `ev-sql-try-5059.database.windows.net` | **AAD-only auth** - no SQL password exists on the server at all |
| SQL Database | `EmergencyVaultDb` | |
| Managed Redis | `ev-redis-5059.westus2.redis.azure.net:10000` | Access keys disabled - Entra ID auth only |
| Static Web App | `emergency-vault` | Frontend (pre-existing) |
| Artifact Signing account | `emergency-vault` | Code-signs the desktop app (see below) |
| Storage account | `evdownloads` | Public `downloads` container mirroring the latest desktop installer(s) + `manifest.json`, for the landing page's download links (repo is private, so GitHub Release assets alone aren't publicly fetchable) |

**Auth chain is fully federated - no long-lived secrets anywhere in the deploy or runtime path:**

- **GitHub Actions -> Azure**: `azure/login` uses OIDC via the `ev-gh-deploy` user-assigned
  identity, federated to `repo:WingZer0o/Application:ref:refs/heads/main`. No client secret in
  GitHub - only the (non-secret) client/tenant/subscription IDs.
- **Container App -> ACR**: pulls the image using its own system-assigned managed identity
  (`AcrPull` role on `evbackendacr`).
- **Container App -> Azure SQL**: `Authentication=Active Directory Managed Identity` in the
  connection string - `Microsoft.Data.SqlClient` acquires the token itself, no code involved. The
  SQL Server has AAD-only auth enabled, so there is no SQL password to leak.
- **Container App -> Redis**: `Microsoft.Azure.StackExchangeRedis` acquires a token for the
  container's system-assigned identity (see `DependencyInjection.cs` - triggered when the Redis
  host ends in `.redis.azure.net`). Access keys are disabled on the cache.
- **GitHub Actions deploy identity -> ACR / Container App**: `AcrPush` on the registry,
  `Container Apps Contributor` scoped to just the `ev-backend` app.
- **GitHub Actions deploy identity -> Artifact Signing / Storage**: `Artifact Signing Certificate
  Profile Signer` scoped to the `emergency-vault` signing account, and `Storage Blob Data
  Contributor` scoped to `evdownloads` - both used only by `build_desktop_installer` (see below).

The only static secrets involved anywhere are the three Infisical bootstrap credentials
(`INFISICAL_CLIENT_ID`/`_SECRET`/`_PROJECT_ID`), stored as Container App secrets - Infisical
supplies everything else (JWT signing key, Resend key, and the SQL/Redis connection strings above)
at startup, same as local dev.

Required values in Infisical's `prod` environment:

```
SQL_SERVER_CONNECTION_STRING=Server=tcp:ev-sql-try-5059.database.windows.net,1433;Database=EmergencyVaultDb;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;
REDIS_CONNECTION_STRING=ev-redis-5059.westus2.redis.azure.net:10000,ssl=True,abortConnect=False
JWT_SIGNING_KEY_PEM=<production ECDSA P-521 key>
RESEND_API_KEY=<production Resend key>
STRIPE_SECRET_KEY=<live-mode Stripe secret key>
STRIPE_PUBLISHABLE_KEY=<live-mode Stripe publishable key>
STRIPE_WEBHOOK_SECRET=<signing secret from the live-mode webhook endpoint in the Stripe Dashboard>
```

To redeploy the backend manually (e.g. outside CI):

```bash
az acr build --registry evbackendacr --image backend:manual --file backend/src/EmergencyVault.Api/Dockerfile --target final backend
az containerapp update -n ev-backend -g Application --image evbackendacr.azurecr.io/backend:manual
```

## Desktop app

`frontend/auth-client` also compiles to a native desktop app via `deno desktop` (WebView2 on
Windows, WKWebView on macOS, WebKitGTK on Linux) - `server.ts` hosts the built Angular app and
proxies `/api/*`, same as in Docker/Azure, just running from a compiled binary instead of behind
nginx/SWA. See `frontend/auth-client/deno.json` for the build/installer tasks and
`frontend/CLAUDE.md` for the exact commands.

`build_desktop_installer` in `deploy.yml` builds, signs and publishes a Windows installer on every
push to `main` that touches `frontend/auth-client/**`, running in parallel with `deploy_frontend`
(both gated on `deploy_backend`):

1. **Version**: auto-incremented from the latest `desktop-v*` git tag (starts at `1.0.0`).
2. **Build**: `ng build` -> `deno task desktop-build:prod` compiles `emergency-vault.exe`, baking
   in the production backend URL (`write-api-target.ts`) since a compiled binary has no env vars
   at launch.
3. **Sign**: both `emergency-vault.exe` and the Inno Setup output are signed via
   `azure/artifact-signing-action` against the `emergency-vault` Artifact Signing account /
   `emergency-vault-public` certificate profile (Individual identity validation - the publisher
   name on the signature is the validated individual's legal name, not a custom brand string;
   Organization validation would be needed to sign as "Emergency Vault" itself). SHA-256 digest,
   RFC3161 timestamped so the signature outlives the certificate's intentionally short (~3-day)
   validity window.
4. **Package**: `Minionguyjpro/Inno-Setup-Action` compiles `installer/EmergencyVault.iss` - a
   per-user install (no UAC prompt) to `%LOCALAPPDATA%`.
5. **Publish**: a GitHub Release (tag `desktop-v<version>`) gets the installer as an asset, *and*
   the same file is uploaded to `evdownloads` blob storage as a fixed-name
   `windows/EmergencyVault-Setup-latest.exe`, with `manifest.json` updated (merged, not
   overwritten, so a future macOS/Linux job can add its own key without clobbering this one). The
   blob mirror exists because the repo is private - GitHub Release assets require an
   `Authorization` header even to fetch the "public" download URL, so the landing page's download
   button points at the public blob container instead.

The landing page's `app-download-links` component (`shared/download-links/`) fetches that
manifest and renders one card per platform the manifest actually has an entry for - macOS/Linux
will show "Coming soon" and light up automatically once a CI job publishes their manifest key, no
frontend changes required beyond the display label in `KNOWN_PLATFORMS`.

The compiled app also knows it's the desktop app: `server.ts` is the only thing that ever serves
`index.html` for it (the deployed website is served as static files by SWA/nginx and never runs
`server.ts`), so it injects a `window.__EV_DESKTOP__` flag that `desktopLandingGuard` checks to
skip the marketing landing page and go straight to `/login` (or `/vault` if a refresh cookie
already restored the session).

Testing a Linux build under WSL2/WSLg works (`deno desktop --target x86_64-unknown-linux-gnu`),
but use the raw directory output rather than `.AppImage` - WSL usually lacks the FUSE support
AppImages need to mount themselves.

## Auth design notes

- **Access token**: short-lived (15 min default), returned in the JSON response body, signed
  with ECDSA P-521 (`ES512`). The signing key is stored in Infisical and injected as the
  `JWT_SIGNING_KEY_PEM` environment variable.
- **Refresh token**: SQL Server is the source of truth (`RefreshTokens` table, rotated on every
  use, hash stored - not the raw token). Redis mirrors `hash -> userId` with a matching TTL for
  fast validation; a Redis miss falls back to SQL Server rather than failing the request.
  Delivered as an `httpOnly`, `Secure`, `SameSite=None` cookie scoped to `/api/auth`, never
  exposed to JavaScript.
- **Roles**: `Admin` and `User`, seeded on startup. `GET /api/users` is `[Authorize(Roles =
  "Admin")]`; promote a user by inserting into the `UserRoles` table (still no UI for this one -
  the `/admin` screens cover billing, not role management).
- **Repository pattern**: applied to `RefreshToken` (`IRefreshTokenRepository`), the entity EF
  Core actually owns here. User/Role persistence goes through ASP.NET Identity's
  `UserManager`/`RoleManager`, which are themselves Microsoft's repository abstraction over EF
  Core - wrapping them again would just be ceremony.
- **Billing paywall**: `VaultController` is gated by the `ActiveSubscription` authorization policy
  (`Api/Authorization/ActiveSubscriptionHandler.cs`) - satisfied by the `Admin` role, an admin-
  granted free-access flag, or an active Stripe subscription (`Subscription` table, one row per
  user). The yearly price lives in the `BillingSettings` singleton row, editable at
  `/admin/billing-settings` with no deploy required. Checkout uses Stripe Checkout in
  `subscription` mode with inline `price_data` (see `BillingService.CreateCheckoutSessionAsync`)
  rather than a synced Stripe Price object, so a price change takes effect on the very next
  checkout.

## Verification checklist

1. `POST /api/auth/register` - console logs a confirmation link.
2. `GET` that link's `userId`/`token` against `/api/auth/confirm-email`.
3. `POST /api/auth/login` - returns an access token and sets the refresh cookie.
4. `GET /api/auth/me` with `Authorization: Bearer <token>` - returns the profile.
5. `POST /api/auth/refresh` (cookie only) - rotates the refresh token.
6. `GET /api/users` - 403 for a plain `User`, 200 after promoting to `Admin`.
7. Decode the access token (e.g. jwt.io) - header should show `"alg": "ES512"`.
8. `GET /api/vault/status` as a fresh `User` with no subscription - 403 (`ActiveSubscription`
   policy). Visiting `/vault` in the browser redirects to `/billing` instead.
9. `POST /api/billing/checkout-session` - returns a Stripe Checkout URL; completing it with
   Stripe's `4242 4242 4242 4242` test card (with `stripe listen` running) fires the webhook and
   flips the user's `Subscription.Status` to `Active` - `/api/vault/status` now returns 200.
10. As `Admin`, `PUT /api/admin/billing/subscriptions/{userId}/free-access` with
    `{ "isFreeAccess": true }` - that user gets vault access without ever completing checkout.
