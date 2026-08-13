# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

Run from `backend/`, or open `EmergencyVault.slnx` in Visual Studio.

```bash
dotnet build EmergencyVault.slnx                       # build everything
dotnet watch --project src/EmergencyVault.Api run       # run API with hot reload (needs sqlserver+redis up)
dotnet ef migrations add <Name> \
  --project src/EmergencyVault.Infrastructure \
  --startup-project src/EmergencyVault.Api              # add an EF Core migration
dotnet ef database update \
  --project src/EmergencyVault.Infrastructure \
  --startup-project src/EmergencyVault.Api              # apply migrations
```

There are no test projects in the solution yet.

Infra dependencies (SQL Server, Redis) run via Docker: `docker compose up sqlserver redis -d` from the repo root. Full dev loop (API + frontend, hot reload) is `docker compose up --build` from the repo root — see the root `README.md` for the three run modes and required `.env`/Infisical configuration.

## Architecture

Clean-architecture layering, dependency flow strictly inward (`Api` → `Application`/`Infrastructure` → `Domain`):

- **`EmergencyVault.Domain`** — entities only (`ApplicationUser`, `ApplicationRole`, `RefreshToken`, `UserFailedLogin`), constants (`CookieConst`, `EnvironmentVariableConst`). No dependencies on other layers.
- **`EmergencyVault.Application`** — DTOs, service interfaces, and the services implementing the actual business logic (`AuthService`, `UserService`). Services return `ServiceResult<T>` (success/data/errors) rather than throwing for expected failure paths. Registered via `AddApplicationServices()`.
- **`EmergencyVault.Infrastructure`** — EF Core (`AppDbContext`, migrations), repositories, Redis caches, email (Hangfire-queued via `ResendEmailSender`), and security primitives (`TokenService`, `HotpService`, `EcdsaKeyProvider`, Infisical secret injection). Registered via `AddInfrastructure()`.
- **`EmergencyVault.Api`** — thin controllers that call into `Application` services and translate `ServiceResult` into HTTP responses; `Program.cs` wires everything up.

### Auth flow specifics

- **Signing key**: loaded once in `Program.cs` from the `JWT_SIGNING_KEY_PEM` env var (Infisical-injected) and registered as a singleton `ECDsa`. `TokenService` (issuing) and the JWT Bearer middleware (validating) both consume this same singleton — never load the key a second time elsewhere, or the two could disagree if the source ever changes.
- **Access token**: ES512, returned in the response body, never stored server-side.
- **Refresh token**: SQL Server (`RefreshTokens` table) is the source of truth; `IRefreshTokenRepository` is the one place the repository pattern is used explicitly (see root README for why). `RedisRefreshTokenCache` mirrors `hash → userId` as a fast-path with a Redis-miss fallback to SQL. Delivered as an httpOnly/Secure/SameSite=Strict cookie scoped to `Path=/api/auth`, rotated on every refresh.
- **Two-factor (HOTP-style email codes)**: `IHotpService` generates the code, `RedisTwoFactorChallengeCache` holds the short-lived challenge (`twoFactorToken → userId`) between `login` and `login/two-factor`. Resend re-uses the same challenge rather than issuing a new one.
- **Identity**: `AddIdentityCore<ApplicationUser>` (not the full `AddIdentity`) + `AddRoles<ApplicationRole>`, keyed by `Guid`. `Roles` are `Admin`/`User`, seeded by `DbInitializer` on startup. There's no admin UI or endpoint to promote users — it's done by inserting into `UserRoles` directly.
- **User identity in controllers**: pull the user id via `User.GetUserId()` (`Extensions/ClaimsPrincipalExtensions.cs`), not by parsing claims manually.

### Background jobs

Hangfire runs against the same SQL Server database (own auto-created schema), with a single worker dedicated to the `"emails"` queue (`EmailJobs`) — this exists specifically to stay under the Resend API rate limit; don't raise `WorkerCount` without checking that constraint first.

### Config

`JwtOptions` and `FrontendOptions` bind from `appsettings*.json` (see `Options/`). `Frontend:BaseUrl` is used to build confirmation/reset links — must match wherever the frontend is actually reachable from. `Cors:AllowedOrigins` must include every origin the browser will load the SPA from (differs between local `ng serve` and the Docker/nginx setups — see root README).

### Production (Azure)

`DependencyInjection.cs` builds both the SQL and Redis connections from plain connection-string env vars, so the same code path handles local Docker and Azure — only the string differs:

- **SQL**: `Microsoft.Data.SqlClient` natively understands `Authentication=Active Directory Managed Identity` in the connection string — no code branch needed. The Azure SQL Server is AAD-only (no SQL password exists).
- **Redis**: no such native support in `StackExchange.Redis`, so the Redis registration in `AddInfrastructure` checks whether the parsed host ends in `.redis.azure.net`; if so it calls `Microsoft.Azure.StackExchangeRedis`'s `ConfigureForAzureWithSystemAssignedManagedIdentityAsync()` before connecting. Local/Docker Redis doesn't match that suffix and connects as a plain connection string like today.

See root README's **Deployment** section for the full resource list and the exact `prod`-environment Infisical values this depends on.
