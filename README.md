# FastCart — Backend API

A unified ASP.NET Core e-commerce backend serving both **storefront/customer** and **admin**
surfaces from one API, separated by role-based authorization. Built with .NET 10, EF Core +
PostgreSQL (Npgsql), ASP.NET Identity, JWT + rotating refresh tokens, and Cloudflare R2 for
image storage.

- **API docs:** Swagger UI at `/swagger` (enabled in every environment).
- **Health:** `GET /health` (used by Render health checks and uptime pings).
- **Versioning:** all endpoints are under `/api/v1`.
- **Response envelope:** every response is `{ success, message, data, errors }`; lists are paged.

---

## 1. Architecture

Clean layering — dependencies point inward:

```
FastCart.Api            ASP.NET Core host: controllers, pipeline, Swagger, auth, rate limiting
  └─ FastCart.Infrastructure   EF Core/Npgsql, Identity, JWT, storage (R2/local), services
       └─ FastCart.Application  contracts + DTOs per feature, common envelope/exceptions
            └─ FastCart.Domain  entities, enums, base types (dependency-free)
```

The schema is created **only** by EF Core migrations. Migrations apply automatically on
startup, and `Admin`/`Customer` roles plus an initial admin are seeded on first run.

---

## 2. Local development

### Prerequisites
- **.NET SDK 10** (`dotnet --version` → `10.0.x`)
- **PostgreSQL** reachable locally (any install works; a user-space install via `scoop` is fine)

### Steps
1. **Start PostgreSQL** and create a database (e.g. `fastcart`).
2. **Configure** `src/FastCart.Api/appsettings.Development.json` (connection string, a dev
   `Jwt:Secret`, and `Seed:AdminEmail`/`Seed:AdminPassword`). In Development a missing JWT
   secret falls back to a placeholder; outside Development the app **fails fast** without one.
3. **Run** the API:
   ```bash
   dotnet run --project src/FastCart.Api
   ```
   Migrations apply and seed data is created on startup.
4. Open **`http://localhost:<port>/swagger`** and **`/health`**.

> Build the whole solution with `dotnet build FastCart.slnx`.
> Add a migration with
> `dotnet ef migrations add <Name> --project src/FastCart.Infrastructure --startup-project src/FastCart.Api`.

---

## 3. Configuration (environment variables)

All deploy-specific values come from environment variables (double-underscore `__` maps to the
nested config key). Never commit real secrets.

| Variable | Purpose |
|----------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string (use Render's **internal** URL) |
| `Jwt__Secret` | access-token signing key (**≥ 32 chars**; required outside Development) |
| `Jwt__Issuer`, `Jwt__Audience` | token issuer / audience |
| `Jwt__AccessTokenMinutes`, `Jwt__RefreshTokenDays` | token lifetimes |
| `Cors__AllowedOrigins` | comma-separated storefront + admin origins (empty = allow any, dev only) |
| `Seed__AdminEmail`, `Seed__AdminPassword` | initial admin, seeded on first run |
| `Storage__R2__Endpoint` | Cloudflare R2 S3-compatible endpoint |
| `Storage__R2__AccessKeyId`, `Storage__R2__SecretAccessKey` | R2 credentials |
| `Storage__R2__Bucket` | R2 bucket name |
| `Storage__R2__PublicBaseUrl` | public base URL used to build stored image URLs |
| `RateLimiting__Auth__PermitLimit`, `RateLimiting__Auth__WindowSeconds` | auth rate limit (default 10 / 60s per IP) |

> **Storage selection:** when the four `Storage__R2__*` core keys are set, images go to
> Cloudflare R2; otherwise the API uses local disk (development only — Render's disk is ephemeral).

---

## 4. Deploy to Render (Docker) + PostgreSQL + Cloudflare R2

The repo ships a `Dockerfile`; Render builds the image remotely (no local Docker needed).
HTTPS/TLS is automatic on `*.onrender.com`.

### 4.1 Create the Cloudflare R2 bucket
1. Cloudflare dashboard → **R2** → **Create bucket** (note the bucket name).
2. **Manage R2 API Tokens** → create a token with read/write → copy the **Access Key ID**,
   **Secret Access Key**, and the **S3 API endpoint** (`https://<accountid>.r2.cloudflarestorage.com`).
3. Enable public access (R2.dev URL or a custom domain) → that URL is your `Storage__R2__PublicBaseUrl`.

### 4.2 Create the database
1. Render dashboard → **New → PostgreSQL** → create the instance.
2. Copy its **Internal Database URL** for `ConnectionStrings__DefaultConnection`.

### 4.3 Create the web service
1. Push this repo to GitHub.
2. Render → **New → Web Service** → connect the repo.
3. **Runtime: Docker** (Render auto-detects the `Dockerfile`; it injects `$PORT`, which the
   container binds to automatically — no port config needed).
4. Add all environment variables from §3 (DB URL, a strong `Jwt__Secret`, R2 keys, seed admin,
   and `Cors__AllowedOrigins` for your frontends).
5. **Health Check Path:** `/health`.
6. **Create Web Service.** On deploy, migrations apply and roles + admin seed automatically.

### 4.4 Verify
- `https://<service>.onrender.com/health` → `200`
- `https://<service>.onrender.com/swagger`
- Log in as the seeded admin and exercise an admin endpoint.

> **Free-tier idle spin-down:** add an external uptime ping (e.g. every ~10 min) against
> `/health` to keep the instance warm.

---

## 5. Security notes
- Short-lived JWT access tokens + rotating, revocable refresh tokens (dead tokens are pruned).
- Role-based authorization on every non-public endpoint, enforced by a secure-by-default
  fallback policy (any unmarked endpoint requires authentication).
- Rate limiting on auth endpoints; anti-enumeration on forgot-password.
- No raw card data is ever stored. CORS is restricted to configured origins in production.
- All money is `decimal`; all timestamps are UTC ISO 8601.
