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
| `Frontend:BaseUrl` | appsettings / `Frontend__BaseUrl` | Used to build email confirmation / password reset links |
| `Cors:AllowedOrigins` | appsettings / `Cors__AllowedOrigins__0`, `__1`, ... | Must include whatever origin the browser loads the frontend from |

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
```

To redeploy the backend manually (e.g. outside CI):

```bash
az acr build --registry evbackendacr --image backend:manual --file backend/src/EmergencyVault.Api/Dockerfile --target final backend
az containerapp update -n ev-backend -g Application --image evbackendacr.azurecr.io/backend:manual
```

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
  "Admin")]`; promote a user by inserting into the `UserRoles` table (no admin UI is scaffolded
  yet).
- **Repository pattern**: applied to `RefreshToken` (`IRefreshTokenRepository`), the entity EF
  Core actually owns here. User/Role persistence goes through ASP.NET Identity's
  `UserManager`/`RoleManager`, which are themselves Microsoft's repository abstraction over EF
  Core - wrapping them again would just be ceremony.

## Verification checklist

1. `POST /api/auth/register` - console logs a confirmation link.
2. `GET` that link's `userId`/`token` against `/api/auth/confirm-email`.
3. `POST /api/auth/login` - returns an access token and sets the refresh cookie.
4. `GET /api/auth/me` with `Authorization: Bearer <token>` - returns the profile.
5. `POST /api/auth/refresh` (cookie only) - rotates the refresh token.
6. `GET /api/users` - 403 for a plain `User`, 200 after promoting to `Admin`.
7. Decode the access token (e.g. jwt.io) - header should show `"alg": "ES512"`.
