# FastCart — Session Log & Resume Notebook

Cold-start handoff. `/start` reads the **▶ RESUME HERE** block first, then `ROADMAP.md`.
`/stop` updates this file. **Safe to clear the chat after running `/stop`.**

---

## ▶ RESUME HERE
- **🎉 ALL PHASES DONE — BACKEND IS LIVE:** `https://fastcart-backend.onrender.com` (Swagger `/swagger`). Phases 0–9 complete & verified on prod. Repo: `https://github.com/Us-user/FastCart-Backend` (branch `main`).
- **Prod admin:** `admin@gmail.com` / `Abuumar5` (seeded; verified login + Admin role + dashboard on prod).
- **Next (optional — project goal is met; pick if continuing):**
  - **(A) Seed/enter catalog data** — DB is empty on prod (no products/categories). Use admin endpoints (or write a seed) to populate so the storefront shows something.
  - **(B) Enable persistent images** — add `Storage__R2__*` env vars (Cloudflare R2) in Render; currently local-disk storage = uploads vanish on redeploy.
  - **(C) Keep-warm** — set an external uptime ping on `/health` (~10 min) to dodge free-tier cold starts (15–50s).
  - **(D) Phase 8 polish (code)** — FluentValidation validators (register/product/checkout/coupon) + Swagger examples + deep N+1 audit. Pure code, no schema change.
- **Uncommitted:** `API-ГАЙД.md` (new RU endpoint reference) is untracked; ROADMAP/this log were just updated. Commit+push if you want them on GitHub.
- **Deploy gotchas (if redeploying / new env):** use Render **External** DB URL, not Internal (bare `dpg-xxx-a` host failed); the app reads `DATABASE_URL` **or** `ConnectionStrings__DefaultConnection`; `Jwt__Secret` must be ≥32 chars or Production fails fast.
- **Each session, before coding (LOCAL dev only):**
  1. **Start PostgreSQL** (it is NOT a Windows service, so it is stopped after a reboot):
     ```powershell
     & "$env:USERPROFILE\scoop\apps\postgresql\current\bin\pg_ctl.exe" `
       -D "$env:USERPROFILE\scoop\apps\postgresql\current\data" `
       -l "$env:USERPROFILE\scoop\apps\postgresql\current\server.log" -o "-p 5432" start
     ```
     (It may already be running — `pg_isready -p 5432` to check.)
  2. **Run the API** (auto-applies migrations + seeds): `dotnet run --project src/FastCart.Api`
     (dev profile → http://localhost:5046). **NB:** `--no-launch-profile` does NOT set the env → JWT secret fail-fast aborts startup; either use the default launch profile or prefix `ASPNETCORE_ENVIRONMENT=Development` (then `--urls http://localhost:5099`).
  3. **Admin login:** `admin@fastcart.local` / `Admin#12345`. Health is at root `/health` (not `/api/v1/health`).
- **Heads-up for next session:** **auth rate limiting is now live (10 reqs / 60s per IP on `/auth/*`).** When smoke-testing, get your token first, then run hammer-tests — you'll hit `429` after 10 auth calls in a window (tune via `RateLimiting__Auth__PermitLimit`). To dry-run the deploy artifact: `dotnet publish src/FastCart.Api/FastCart.Api.csproj -c Release -o <dir>` then run the DLL with `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://0.0.0.0:8080`, a 32+ char `Jwt__Secret`, and the connection string.

---

## How to run / build / verify
- **Stack:** .NET 10 · EF Core + Npgsql · PostgreSQL 18.4 (local via scoop, db `fastcart`, `postgres`/`postgres`).
- **Build:** `dotnet build FastCart.slnx`
- **New migration:** `dotnet ef migrations add <Name> --project src/FastCart.Infrastructure --startup-project src/FastCart.Api` (app applies on startup).
- **Smoke tests:** `curl` here is mingw — for file uploads, `cd` into a dir and use **bare filenames** (`-F "images=@file.png"`), not `/tmp/...` (mingw mangles the path → curl exit 26).

## Architecture map
- **Domain** (`src/FastCart.Domain`): `Entities/*Entities.cs`, `Enums/Enums.cs`, `Common/` (BaseEntity, IAuditable, Roles).
- **Application** (`src/FastCart.Application`): contracts + DTOs per feature — `Auth`, `Profile`, `Addresses`, `Catalog`, `Carts`, `Wishlists`, `Reviews`, `Coupons`; plus `Common/` (ApiResponse, PagedResult, Exceptions, Interfaces).
- **Infrastructure** (`src/FastCart.Infrastructure`): `Persistence/` (AppDbContext, DbSeeder, AppDbContextFactory, Migrations), `Identity/` (ApplicationUser, AuthService, JwtTokenGenerator, Profile/Address services), `Catalog/` (Taxonomy + Product services), `Commerce/` (Cart/Wishlist/Review/Coupon services), `Storage/` (Local + R2), `Messaging/`; `DependencyInjection.AddInfrastructure`.
- **Api** (`src/FastCart.Api`): `Program.cs` (pipeline, JWT + enveloped 401/403, Swagger, startup migrate/seed), `Controllers/`, `Middleware/ExceptionHandlingMiddleware`, `Common/` (ImageValidation, ClaimsPrincipalExtensions).

## Conventions
- Response envelope `{success,message,data,errors}` (ApiResponse); `PagedResult<T>` for lists (pageNumber/pageSize/totalCount).
- Throw `AppException` subclasses → middleware maps: Validation 400 / Unauthorized 401 / Forbidden 403 / NotFound 404 / Conflict 409 / BusinessRule 422.
- Routes: `BaseApiController` = `api/v1/[controller]`; admin writes `[Authorize(Roles = Roles.Admin)]`; reads public. Enums serialize as strings.
- Money `decimal(18,2)`; UTC timestamps stamped automatically in `SaveChanges`.

## Phases done (one-liners)
- **P0** infra: layered solution, envelope + exception middleware, Swagger, `/health`, CORS, JWT scheme.
- **P1** data: all §5 entities + `Review`; AppDbContext; `InitialCreate` (36 tables) applied; roles + admin seeded.
- **P2** auth: register / login(email|phone) / refresh(rotating) / logout / forgot / reset / change / me; profile (multipart image); addresses CRUD + default; role auth; enveloped 401/403.
- **P3** catalog: storage (local + R2); taxonomy CRUD; products w/ options+values+variants+images; variant-aware list (filter/sort/page/swatches); detail + related; admin variant/option/image mgmt; image validation; variant-combo uniqueness.
- **P4** commerce: cart (variant, computed totals); wishlist (+ move-all-to-cart); reviews (+ summary, owner/admin delete); coupons (validate rules + admin CRUD).
- **P5** orders: payment providers (`IPaymentProvider` + resolver; Manual/Bank record-and-hold + CashOnDelivery); `POST /orders/checkout` (atomic txn, snapshots incl. `UnitCost`, coupon redemption, stock decrement, cart clear); customer orders list/detail/cancel/return/`/returns`/pay; admin orders list/detail/offline-create/set-status(transition-enforced)/set-payment-status; admin returns approve/reject/complete (restore stock + refund).
- **P6** cms/admin: sliders (public active + admin CRUD, multipart); banners (public active w/ expiry filter + admin CRUD, categoryId+endsAt); newsletter (idempotent subscribe + admin list); contact (create + admin list); admin users/roles (list/detail/delete + assign/remove role + list roles). Delete-user blocks on Restrict FKs (redemptions/returns→409) + self-delete/self-admin-removal guards (422).
- **P7** dashboard (§6.15): `summary?from=&to=` (Sales/Cost/Profit from line snapshots, excl. Cancelled/Returned), `revenue?year=` (12 months zero-filled, `Order.Total`), `top-products?metric=sales|units&take=`, `recent-transactions?take=`. New code only.
- **P8 (core)** hardening: auth rate limiting (429); secure-by-default `FallbackPolicy`; global UTC `DateTime` JSON converter + `DateTimeExtensions.ToUtc()`; InvariantCulture money; refresh-token pruning; HttpLogging + Production JSON console (EF SQL quieted).
- **P9 (prep)** deploy: `Dockerfile` (multi-stage, `$PORT`), `.dockerignore`, `README.md`; migrate-on-deploy already wired. Image build unverified locally (no Docker); validated via publish + Production dry-run.
- **P9 (LIVE)** deployed to Render: `https://fastcart-backend.onrender.com`. Git repo + GitHub push; `postgres://`→Npgsql + `DATABASE_URL` fallback fixes; admin `admin@gmail.com`/`Abuumar5`. R2 skipped (local-disk/ephemeral); prod DB empty. `API-ГАЙД.md` (RU reference).

## Decisions / deviations from TZ
- `ApplicationUser`/`ApplicationRole` live in Infrastructure (Domain stays dependency-free).
- `reset-password` request also carries `email` (needed to resolve the user).
- `Review` entity added (implied by §6.6, absent from the §5 table).
- `POST /products` multipart: `options`/`variants` sent as JSON-string fields, `images` as files.
- Reviews allow multiple per user (D4 "post freely").

## Carried-forward issues (route to the right phase)
- **RESOLVED (P8):** auth rate-limiting (fixed-window 429); prune revoked/expired refresh tokens (`ExecuteDeleteAsync` on issue); money messages now InvariantCulture (`"20.00"`); `Unspecified→timestamptz` date bug (global JSON converter + query-string `.ToUtc()`).
- **Deferred polish (P8):** full FluentValidation migration; Swagger request/response examples; deep N+1/perf audit. (Auth **lockout** not added — rate limiting covers the brute-force surface.)
- **Minor (open):** `Storage:R2:PublicBaseUrl` hardcoded to `:5046` in dev (slider/banner `imageUrl`s show `:5046` regardless of run port); `reset-password` returns 422 for an unknown email.
- **RESOLVED (P6):** admin delete-user `RESTRICT` FKs — now blocks redemptions/returns with 409 + message; orders set-null; rest cascade.

## Open TZ items (§10) — still at defaults; confirm when each becomes relevant
D8 tax (rate 0, no line) · D4 reviews (all customers) · D5 currency (USD) · D6 condition in / features deferred · D11 Google sign-in & saved payments (stubs) · D13 variant subset (allowed).

---

## Session history

## Session 05 — 2026-06-25
**Phase:** Phase 9 (deployment) — **DEPLOYED & LIVE.** All phases (0–9) complete.
**Done this session:**
- **Deployed to production:** live at `https://fastcart-backend.onrender.com` (Swagger `/swagger`). Render Web Service (Docker build from repo) + Render PostgreSQL.
- **Git:** initialized repo (was not under git), added `.gitignore`, **3 commits**, pushed to GitHub `https://github.com/Us-user/FastCart-Backend` (branch `main`). `appsettings.Development.json` kept in repo (dev-only values, no real secrets).
- **Two deploy fixes (code, shipped):**
  - `Infrastructure/DependencyInjection.cs` `BuildConnectionString()` — auto-converts `postgres(ql)://user:pass@host[:port]/db` URLs to Npgsql key-value form + `SslMode=Require;TrustServerCertificate=true` (managed hosts give URL form).
  - `Infrastructure/DependencyInjection.cs` `ResolveRawConnectionString()` — resolves `ConnectionStrings:DefaultConnection`, else falls back to conventional `DATABASE_URL` env var. Used by DbContext registration **and** `Program.cs` startup migrate/seed check.
- **Verified on prod:** `/health` 200; `/products` & `/categories` 200 (DB connected, migrations applied, tables empty); admin login `admin@gmail.com`/`Abuumar5` → JWT w/ Admin role → `/admin/dashboard/summary` 200.
- **Docs:** `API-ГАЙД.md` — full Russian endpoint reference (20 sections: envelope, auth/roles, every route grouped w/ access level + purpose, JS examples, HTTP codes). *(untracked — not yet committed)*
**Decisions / deviations from TZ:**
- **R2 skipped for now** — deployed without `Storage__R2__*`, so storage = local disk (ephemeral on Render; uploaded images won't survive redeploys). Add R2 env vars later to enable persistence.
- Connection string read from `DATABASE_URL` as well as `ConnectionStrings__DefaultConnection` (robustness for managed hosts).
**Known issues / blockers:**
- **Prod DB is empty** — no catalog seeded (only roles + admin). Storefront has nothing to show until data is added.
- **Free-tier cold start** (~15–50s after 15 min idle) — no uptime ping configured yet.
- **Images not persistent** until R2 is configured.
- `API-ГАЙД.md` + updated ROADMAP/session-log are uncommitted (no commit/push done this turn per /stop rules).
**Deploy gotchas hit (record for future):**
- `ConnectionStrings__DefaultConnection` env var wasn't picked up by the running container → used `DATABASE_URL` instead (single word, typo-proof).
- Render **Internal** DB URL (bare host `dpg-d8u43grtqb8s73aqhtr0-a`) failed to connect → **External Database URL** (`...-a.oregon-postgres.render.com`) worked (likely region/private-network mismatch).
**Next steps (exact):**
1. (Optional) Seed/enter catalog data via admin endpoints so the storefront is non-empty.
2. (Optional) Add `Storage__R2__*` env vars in Render for persistent image storage.
3. (Optional) Configure an external uptime ping on `/health` to avoid cold starts.
4. (Optional) Phase 8 polish: FluentValidation validators + Swagger examples + N+1 audit.

## Session 04 — 2026-06-25
**Phase:** Phase 7 (dashboard) **complete & verified**; Phase 8 **core hardening complete** (polish deferred); Phase 9 **deploy prep complete** (live deploy needs user accounts). **Next = Phase 8 polish (A) or Phase 9 live deploy (B).**
**Done this session:**
- **Phase 7 — Admin dashboard (§6.15):** `Application/Dashboard/DashboardContracts.cs` (`IDashboardService` + DTOs), `Infrastructure/Dashboard/DashboardService.cs`, `Api/Controllers/AdminDashboardController.cs`; DI-registered. No migration. Endpoints: `summary?from=&to=`, `revenue?year=`, `top-products?metric=sales|units&take=`, `recent-transactions?take=`. **Verified live** (incl. 401 w/o admin token, date-range, empty/future ranges).
  - **2 EF bugs found+fixed via live test:** (1) `top-products` ordered by projected record props after `GroupBy` → not translatable; fixed by ordering the `IGrouping` by aggregates *before* the constructor projection. (2) `summary?from/to` query-string dates were `Kind=Unspecified` → Npgsql `timestamptz` reject; fixed (see Phase 8 global fix).
- **Phase 8 — core hardening (all verified live):**
  - **Auth rate limiting:** `Program.cs` `AddRateLimiter` fixed-window policy `"auth"` (cfg `RateLimiting:Auth:PermitLimit`=10 / `WindowSeconds`=60, per remote IP), enveloped `429` + `Retry-After`; `[EnableRateLimiting("auth")]` on `AuthController`. Verified: `401×10` then `429`.
  - **Secure-by-default authz:** `FallbackPolicy = RequireAuthenticatedUser()`; `[AllowAnonymous]` added to `HealthController`. Verified public reads still 200, protected → enveloped 401.
  - **Global UTC `DateTime` JSON converter:** `Api/Common/UtcDateTimeJsonConverter.cs` (+ nullable), registered in `AddJsonOptions`. Fixes the `Unspecified→timestamptz` write bug for ALL body dates. Verified: coupon w/ date-only `startsAt`/`expiresAt` now persists (`2026-01-01T00:00:00Z`).
  - **Query-string date UTC:** shared `Application/Common/DateTimeExtensions.ToUtc()`; used in `AdminOrderService` (list from/to) + `DashboardService` (replaced its private helper).
  - **InvariantCulture money:** `CouponService.cs:46` min-order message → `20.00` (was `20,00`).
  - **Refresh-token pruning:** `AuthService.IssueTokensAsync` runs `ExecuteDeleteAsync` of the user's revoked/expired tokens before issuing.
  - **Structured logging:** `AddHttpLogging` (method/path/status/duration) + `app.UseHttpLogging()`; JSON console logging in Production; EF `Database.Command` logs quieted to Warning (was an Info SQL firehose). Verified in prod dry-run.
- **Phase 9 — deploy prep:** `Dockerfile` (multi-stage `sdk:10.0`→`aspnet:10.0`, cached restore, binds Render `$PORT` default 8080 via `sh -c` entrypoint), `.dockerignore`, `README.md` (architecture + local-dev + §9.4 env table + R2→Render click-by-click). Migrate-on-deploy already wired in `Program.cs`.
  - **Docker image build NOT run locally** (Docker not installed). Validated instead: `dotnet publish -c Release` (produces `FastCart.Api.dll`) + **Production dry-run of the published DLL** on `:8080` → boots Production (JWT-secret fail-fast satisfied), `/health` 200, JSON logs, `MigrateAsync` ran (`already up to date`), 0 SQL Info lines after the logging fix.
**Decisions / deviations from TZ:**
- Dashboard counts all order statuses except `Cancelled`/`Returned` for Sales/Cost/Profit (in-progress orders book as sales). `summary.sales` = gross from line snapshots; `revenue.total` = `Order.Total` (net of discount) — intentionally different figures.
- **Phase 8 FluentValidation deferred:** DataAnnotations + enveloped 400 already satisfy §8 "all bodies validated"; FV would be a stylistic migration. Swagger examples + deep N+1 audit also deferred (schemas auto-generate; indexes/pagination/Include already present).
**Known issues / blockers:**
- None blocking. **Live deploy (Phase 9) needs the user's Cloudflare R2 + Render accounts.** Docker image itself is unverified until a Render build runs.
- Minor (cosmetic, open): `Storage:R2:PublicBaseUrl` `:5046` in dev; reset-password 422 for unknown email.
**Next steps (exact):**
1. **(A) Phase 8 polish** — FluentValidation validators for register / product-create / checkout / coupon bodies; Swagger request/response examples; optional deep N+1/perf audit. Pure code, no schema change.
2. **(B) Phase 9 live deploy** — create R2 bucket (capture endpoint/keys/public URL); create Render PostgreSQL (copy internal conn string) + Web Service (Docker); set §9.4 env vars incl. strong `Jwt__Secret`; deploy; verify `/health` + `/swagger`; add uptime ping. Steps in `README.md`.

## Session 03 — 2026-06-24
**Phase:** Phase 6 (CMS/content, newsletter, contact, admin users) — **complete & verified live**; **next = Phase 7** (admin dashboard & reporting, §6.15).
**Done this session:**
- **Sliders (§6.12):** `Application/Content/ContentContracts.cs` (`ISliderService`/`IBannerService` + DTOs/inputs); `Infrastructure/Content/ContentServices.cs` (`SliderService`, `BannerService`); `Api/Controllers/ContentControllers.cs` — public `GET /sliders` (active, ordered) + `AdminSlidersController` (`/admin/sliders` GET-all/GET/POST/PUT/DELETE, multipart image, image required on create→400).
- **Banners (§6.12):** public `GET /banners` (active AND `EndsAt==null||>now`) + `AdminBannersController` (multipart, optional `categoryId` validated→404, `endsAt` countdown).
- **Newsletter + Contact (§6.13):** `Application/Communications/CommunicationContracts.cs`; `Infrastructure/Communications/CommunicationServices.cs` (`NewsletterService` idempotent + lowercased email, `ContactService`); `Api/Controllers/CommunicationsControllers.cs` — public `POST /newsletter/subscribe`, `POST /contact`; admin `GET /admin/newsletter`, `GET /admin/contact-messages` (paged).
- **Admin users/roles (§6.14):** `Application/AdminUsers/AdminUserContracts.cs`; `Infrastructure/Identity/AdminUserService.cs` (UserManager+RoleManager+AppDbContext); `Api/Controllers/AdminUsersController.cs` (+`AdminRolesController`) — `/admin/users` list(filter/page)/detail(+profile)/delete, `/{id}/roles` assign/remove, `/admin/roles` list.
- Registered 5 services in `Infrastructure/DependencyInjection.cs` (Phase 6 block). Build green (0 errors; 2 pre-existing NU1510 warnings). **No migration** — entities already existed from Phase 1.
- **Verified live (local PG, ran on :5099):** slider create+active list+missing-image 400+no-token 401; banner invalid-category 404 + expired hidden from public/visible to admin; newsletter idempotent case-insensitive + bad-email 400 + admin list; contact create + validation 400 + admin list; admin roles list; user register→list→detail; role assign/remove idempotent; delete-no-deps 200→404; missing-user 404; **delete user w/ redemption→409, w/ return→409 (both still exist after)**; self-delete→422; self Admin-role-removal→422.
**Decisions / deviations from TZ:**
- Delete-user is **block-with-message** (not anonymize): redemptions/returns → 409. Two extra safety guards added (self-delete, self Admin-role-removal → 422) — beyond TZ but sensible.
- Newsletter subscribe normalizes email to lowercase + trims; idempotent (re-subscribe → 200, no dupe). Contact trims fields.
- Banner public list excludes expired (`EndsAt` elapsed); admin list shows all. Slider/banner `IsActive` defaults true on create when form omits it.
**Known issues / blockers:**
- None blocking. → **Phase 8:** rate-limiting, prune revoked refresh tokens, money-message culture. Minor: `PublicBaseUrl :5046` cosmetic (image URLs show :5046 regardless of run port); reset-password 422 for unknown email.
**Next steps (exact):**
1. Phase 7 — `GET /admin/dashboard/summary?from=&to=`: Sales/Cost/Profit from `OrderItem.UnitPrice`/`UnitCost` snapshots (D9); decide which order statuses count (likely exclude Cancelled/Returned).
2. `GET /admin/dashboard/revenue?year=` — monthly revenue + order counts.
3. `GET /admin/dashboard/top-products?metric=sales|units&take=`.
4. `GET /admin/dashboard/recent-transactions?take=`. All new code; no schema change.

## Session 02 — 2026-06-24
**Phase:** Phase 5 (checkout & orders, payments, returns) — **complete & verified live**; **next = Phase 6** (CMS/content, newsletter, contact, admin users).
**Done this session:**
- **Payments (§7.3):** `Application/Payments/PaymentContracts.cs` (`IPaymentProvider`, `IPaymentProviderResolver`, `PaymentChargeRequest`/`PaymentResult`); `Infrastructure/Payments/` `CashOnDeliveryPaymentProvider`, `ManualPaymentProvider` (Bank; `Payments:Bank` `AutoMarkPaid`/`SimulateFailure` toggles), `PaymentProviderResolver`. Record-and-hold → Pending (D2/D3, no card data).
- **Customer orders (§6.10/§7.1):** `Application/Orders/OrderContracts.cs`; `Infrastructure/Orders/OrderService.cs` — `POST /orders/checkout` in one EF transaction; list (`?status=`), detail, cancel (stock restore), return, pay; `Api/Controllers/OrdersController.cs` (+ `ReturnsController` for `GET /returns`).
- **Admin orders (§6.11/§7.2/§7.4):** `Application/Orders/AdminOrderContracts.cs`; `Infrastructure/Orders/AdminOrderService.cs` — list (filter/sort/page), detail, offline create, set-status (transition-enforced), set-payment-status, returns list + approve/reject/complete; `Api/Controllers/AdminOrdersController.cs` (+ `AdminReturnsController`).
- **Shared:** `Infrastructure/Orders/OrderingHelpers.cs` (order-number gen, variant description, stock restore, `MapOrder`); registered all in `DependencyInjection.cs`.
- **Verified live (local PG):** COD + Bank checkout; coupon 10% ($29.98→$26.98) + redemption + `TimesUsed` bump; cancel restores stock; empty-cart→422; admin New→Ready→Shipped→Received; invalid New→Shipped→422; return request→approve→complete → order `Returned`/`Refunded` + stock restored; offline order userless/`Paid` with `UnitCost` snapshot. Stock math exact (variant 1: 50→…→47).
**Decisions / deviations from TZ:**
- Admin **offline orders skip coupons** (`CouponRedemption.UserId` is non-null `RESTRICT` FK; offline orders have null UserId).
- Order number = `#` + random 6-digit, uniqueness-retried.
- Pay/`PUT payment-status` reflect status onto the latest `Payment` row; checkout uses `AsSplitQuery` for nested cart includes.
**Known issues / blockers:**
- None blocking. Deferred → **Phase 6:** admin delete-user `RESTRICT` FKs (now also includes orders/returns/redemptions). → **Phase 8:** rate-limiting, prune revoked refresh tokens, money-message culture. Minor: `PublicBaseUrl :5046` dev cosmetic; reset-password 422 for unknown email.
**Next steps (exact):**
1. Phase 6 — Sliders: public `GET /sliders` + admin `/admin/sliders` CRUD (multipart image → `IStorageService`).
2. Banners: public `GET /banners` + admin `/admin/banners` CRUD (countdown `endsAt`, optional `categoryId`).
3. Newsletter subscribe + admin list (§6.13); Contact create + admin list (§6.13).
4. Admin users & roles: list/detail/delete (handle `RESTRICT` FKs), assign/remove role, list roles (§6.14).

## Session 01 — 2026-06-24
**Phase:** Phases 1–4 complete & verified live; **next = Phase 5** (checkout & orders, payments, returns).
**Done this session:**
- Stood up local **PostgreSQL 18.4** (scoop, user-space, no admin) — db `fastcart`, `postgres`/`postgres`, `:5432`.
- **P1:** all §5 entities + `Review`; `AppDbContext`; `InitialCreate` (36 tables) applied; roles + admin seeded.
- **P2:** `/auth` register/login(email|phone)/refresh(rotating)/logout/forgot/reset/change/me; `/profile` (multipart image); `/addresses` CRUD + default; role auth.
- **P3:** storage (Local + R2); taxonomy CRUD; products w/ options+values+variants+images; variant-aware list/detail/related; admin variant/option/image mgmt.
- **P4:** cart (variant + totals); wishlist (+ move-all); reviews (+ summary, owner/admin delete); coupons (validate + admin CRUD).
- Review fixes **#1–#4**: enveloped 401/403, image validation, variant-combo uniqueness, JWT secret fail-fast — all verified live.
- Created `/start` + `/stop` commands, `ROADMAP.md`, and this notebook.
**Decisions / deviations from TZ:**
- ApplicationUser/Role in Infrastructure; `reset-password` carries `email`; `Review` entity added; `POST /products` multipart (options/variants as JSON strings, images as files); reviews allow multiple per user (D4).
**Known issues / blockers:**
- None blocking. Deferred → **Phase 8**: rate-limiting/lockout, prune revoked refresh tokens, money-message culture (`"20,00"`). → **Phase 6**: admin delete-user `RESTRICT` FKs. Minor: `PublicBaseUrl :5046` dev cosmetic; reset-password 422 for unknown email.
- **Start PostgreSQL each session** (not a service).
**Next steps (exact):**
1. `IPaymentProvider` abstraction + Manual/Test provider (record-and-hold, D2/D3); CashOnDelivery (§7.3).
2. `POST /orders/checkout` — single atomic txn: validate variant active + `StockCount ≥ qty` (409), recompute effective prices server-side, apply coupon via `CouponService.Evaluate`, snapshot `OrderItem`s (name/sku/variant desc/UnitPrice/**UnitCost**), create `Payment` (Pending), decrement stock, write `CouponRedemption` + bump `TimesUsed`, clear cart (§7.1).
3. Customer orders: list/detail/cancel/return/pay (§6.10). Admin orders: list/detail/create-offline/set-status/set-payment-status; returns approve/reject/complete (§6.11). Stock restore on Cancelled / completed Returned (§7.4).
