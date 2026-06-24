# FastCart Backend — Build Roadmap

Derived from `FastCart-Backend-Technical-Specification-v1.2.md` (the TZ).
This is the **source of truth for progress**. Check items off as they land.
Use `/start` to resume work and `/stop` to save progress.

**Stack (confirmed from environment):** .NET 10 (SDK 10.0.204) · EF Core + Npgsql/PostgreSQL · ASP.NET Identity · JWT + refresh · Cloudflare R2 (S3 SDK) · Swagger · Docker · Render.

**Legend:** `[ ]` todo · `[~]` in progress · `[x]` done · `[!]` blocked/needs decision

---

## Phase 0 — Solution scaffold & infrastructure
Goal: an empty-but-runnable layered solution that boots, shows Swagger, and answers `/health`.

- [x] Create solution `FastCart.slnx` with 4 projects: `FastCart.Api`, `FastCart.Application`, `FastCart.Domain`, `FastCart.Infrastructure` (clean-architecture layering, §4.1) *(.NET 10 emits the new `.slnx` format)*
- [x] Wire project references (Api→Application→Domain; Infrastructure→Application/Domain; Api→Infrastructure for DI)
- [x] Add NuGet packages: Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.AspNetCore.Authentication.JwtBearer, AWSSDK.S3, Swashbuckle, FluentValidation
- [x] `appsettings.json` + `appsettings.Development.json` + env-var binding for all §9.4 config keys
- [x] Standard response envelope `{ success, message, data, errors }` + paged-result type (§4.3)
- [x] Global exception handler → maps to 400/401/403/404/409/422 with envelope (§4.3) *(maps thrown `AppException`s + model-state 400; raw auth 401/403 reshaping deferred to §8 hardening)*
- [x] API versioning at `/api/v1` (URL segment) *(via `BaseApiController` route prefix)*
- [x] Swagger/OpenAPI at `/swagger` with JWT bearer auth button
- [x] `GET /health` endpoint (§6.16)
- [x] CORS policy from `Cors__AllowedOrigins` (§8)
- [x] **Milestone:** `dotnet run` boots, `/swagger` loads, `/health` returns 200

## Phase 1 — Domain model, DbContext, migrations, seeding  ✅ DONE
Goal: every entity from §5 exists, migrations build the schema, roles + admin seed.

- [x] Enums (§5.7): OrderStatus, PaymentStatus, PaymentMethod, DiscountType, ProductCondition, ReturnStatus (Role = Identity roles + `Domain.Common.Roles`)
- [x] Identity & users: `ApplicationUser`/`ApplicationRole` (Infrastructure), `UserProfile`, `RefreshToken`, `Address` (§5.1); email-or-phone login (Phase 2 auth service)
- [x] Catalog entities: Category, SubCategory, Brand, Color, Tag, Product, ProductImage, ProductTag (§5.2)
- [x] **Variant model (core of v1.2):** ProductOption, ProductOptionValue, ProductVariant, ProductVariantOptionValue (§5.2, D1/D13)
- [x] Cart, CartItem (variant-based), WishlistItem (§5.3)
- [x] Coupon, CouponRedemption (§5.4)
- [x] Order, OrderItem (snapshots incl. UnitCost), Payment, ReturnRequest (§5.5, D9)
- [x] Content/misc: Slider, Banner, NewsletterSubscriber, ContactMessage (§5.6)
- [x] **Review** entity added (implied by §6.6, absent from §5 table)
- [x] Audit fields (CreatedAt/UpdatedAt UTC) on all entities via `IAuditable` + `SaveChanges` override (§5)
- [x] `AppDbContext` (Identity-derived) + entity configs; unique indexes (Variant.Sku, Coupon.Code, Newsletter.Email, OrderNumber, one-cart-per-user, wishlist UserId+ProductId); FK + filter indexes (§8)
- [x] First EF Core migration `InitialCreate` (36 tables); **applied** to local PostgreSQL (PG 18.4 via scoop)
- [x] Seed `Admin`/`Customer` roles + initial admin from config; auto-migrate + seed on startup
- [x] **Milestone:** migration creates full schema; app seeds roles + admin on first run — *verified live*

## Phase 2 — Auth, profile, addresses  ✅ DONE
Goal: full account lifecycle with JWT + rotating refresh tokens.

- [x] JWT issuance (access ≈15–60 min) + rotating, revocable refresh tokens (§4.4)
- [x] `/auth`: register, login (email/phone), refresh, logout, forgot-password (always 200), reset-password, change-password, me (§6.1)
- [x] Password reset via Identity email-token flow; email sender abstraction (dev logger) (§4.4)
- [x] `/profile` GET + PUT (multipart w/ image via storage) (§6.2)
- [x] `/addresses` CRUD + set-default (§6.3)
- [x] Role-based auth policies (Customer/Admin) + ownership checks; enveloped 401/403 (§4.4)
- [~] Anti-enumeration on forgot-password ✅ · **rate limiting deferred to Phase 8**
- [x] **Milestone:** register → login → authenticated endpoint → refresh → logout — *verified live (incl. token rotation/revocation)*

## Phase 3 — Catalog: taxonomy + products/variants + images  ✅ DONE
Goal: full product catalog with the variant model and R2 image storage.

- [x] Storage abstraction (`IStorageService`) + Cloudflare R2 impl (AWS S3 SDK); local-disk impl for dev; auto-selected by config (§9.3, D12)
- [x] Taxonomy CRUD: categories (multipart image), subcategories, brands, colors (hex), tags (§6.4)
- [x] `POST /products` — create product **with options + values + variants + images** in one call (§6.5)
- [x] `GET /products` — filtering/sorting/pagination, variant-aware (fromPrice, price range, inStock, swatches, avg rating) (§6.5)
- [x] `GET /products/{id}` — full detail incl. option axes/values + variant list; `/related`
- [x] Product update/delete, bulk-delete, image add/remove (§6.5)
- [x] Variant management endpoints incl. quick stock update; option-axis management (§6.5, D13)
- [x] Image validation (type/size); variant-combination uniqueness; enums serialized as strings (review fixes)
- [x] **Milestone:** create a multi-variant product with images, then filter/sort/paginate it — *verified live*

## Phase 4 — Cart, wishlist, reviews, coupons  ✅ DONE
Goal: pre-checkout commerce features.

- [x] `/cart` (variant-based): get, add, set qty, increment, decrement, remove, clear; computed totals (§6.7) — *verified live*
- [x] `/wishlist` (product-level): list, add, remove, move-all-to-cart (§6.8) — *verified live (single-variant added, multi-variant flagged)*
- [x] Reviews: list + summary, create (1–5, any customer D4), delete (admin/owner) (§6.6) — *verified live (incl. 403 for non-owner)*
- [x] Coupons: `POST /coupons/validate`; admin coupon CRUD (§6.9); coupon rules engine (§7.5) — *verified live*
- [x] **Milestone:** add variants to cart, apply a coupon, see correct computed totals — *verified live (10% capped at max, min-order enforced)*

## Phase 5 — Checkout & orders, payments, returns  ✅ DONE
Goal: the full order lifecycle with transactional checkout.

- [x] `IPaymentProvider` abstraction + Manual/Test provider (record-and-hold, D2/D3; `Payments:Bank` `AutoMarkPaid`/`SimulateFailure` toggles); CashOnDelivery (§7.3) — *verified live*
- [x] `POST /orders/checkout` — atomic transaction, server-side recompute, stock decrement, snapshots (incl. `UnitCost`), coupon redemption, clear cart (§7.1) — *verified live*
- [x] Customer orders: list (incl. `?status=Cancelled`), detail, cancel, return, `/returns`, pay (§6.10) — *verified live*
- [x] Order status lifecycle + stock restore on cancel/return (§7.2, §7.4) — *verified live*
- [x] Admin orders: list/filter/sort/page, detail, create offline order, set status (transition-enforced), set payment-status (§6.11) — *verified live*
- [x] Returns: customer request + admin approve/reject/complete; refund + stock restore on completion (§6.11, §7.4) — *verified live*
- [x] **Milestone:** cart → checkout → order (Pending) → admin marks Paid → ship → received; return restores stock + refunds; cancel restores stock — *verified live*

## Phase 6 — CMS/content, newsletter, contact, admin users  ✅ DONE
Goal: home-page content and remaining admin surfaces.

- [x] Sliders: public `GET /sliders` (active) + admin `/admin/sliders` CRUD (multipart image; missing-image→400) (§6.12) — *verified live*
- [x] Banners (countdown/flash sales): public `GET /banners` (active, expired filtered) + admin `/admin/banners` CRUD (categoryId→404 if missing, `endsAt`) (§6.12) — *verified live*
- [x] Newsletter subscribe (idempotent, case-insensitive) + admin list (§6.13) — *verified live*
- [x] Contact message create + admin list (§6.13) — *verified live*
- [x] Admin users & roles: list (filter/page), detail (+profile), delete, assign/remove role, list roles (§6.14) — *verified live*
- [x] **Carried-forward fix:** delete-user RESTRICT FKs — orders set-null (kept); profile/addresses/cart/wishlist/reviews/tokens cascade; **coupon redemptions / return requests block with 409 + specific message**; guards block self-delete (422) and self Admin-role-removal (422) — *verified live*
- [x] **Milestone:** all CMS + admin-user endpoints functional and authorized — *verified live*

## Phase 7 — Admin dashboard & reporting  ✅ DONE
Goal: dashboard analytics off order-line snapshots.

- [x] `summary?from=&to=` (Sales/Cost/Profit from UnitPrice/UnitCost snapshots, D9) (§6.15) — *verified live*
- [x] `revenue?year=` (monthly revenue + order counts; 12 months, zero-filled) — *verified live*
- [x] `top-products?metric=sales|units&take=` — *verified live (both metrics)*
- [x] `recent-transactions?take=` — *verified live*
- [x] **Milestone:** dashboard returns correct profit even after later cost edits — *verified live (Sales gross-from-lines vs revenue net-of-discount distinction confirmed)*

## Phase 8 — Cross-cutting hardening  🟡 CORE DONE (polish deferred)
Goal: production-readiness per §8.

- [~] FluentValidation on all request bodies; documented validation errors (§8) — **deferred:** all bodies already validated via DataAnnotations + enveloped 400 (§8 "all bodies validated" met); FV would be a stylistic migration
- [x] Security pass: HTTPS, role auth on every non-public endpoint, ownership checks, no raw card data (D3), CORS locked (§8) — *secure-by-default fallback authz policy + auth rate limiting added; verified live*
- [~] Performance pass: indexes, pagination everywhere, kill N+1 via Include/projection, efficient variant aggregates (§8) — indexes (P1), pagination, Include/projection/AsNoTracking already in place; **no dedicated deep N+1 audit done**
- [x] Structured logging / observability (§8) — *request logging (method/path/status/duration) + JSON console in Production; verified live*
- [~] Swagger polish: full request/response schemas, examples (§4.2) — schemas auto-generate; **examples deferred**
- [~] **Milestone:** clean security + perf review against §8 checklist — security/observability done; FV + Swagger-examples + deep-perf audit remain

## Phase 9 — Deployment (Render + PostgreSQL + R2)  ✅ LIVE
Goal: live on Render with Swagger reachable. **DONE — live at `https://fastcart-backend.onrender.com`.**

- [x] `Dockerfile` for reproducible build (§4.2) — multi-stage SDK10→aspnet10, layer-cached restore, binds Render `$PORT`; `.dockerignore` added. **Built successfully by Render.**
- [x] Auto-apply migrations on deploy (startup hook or pre-deploy) (§9.2) — startup `MigrateAsync`+seed; confirmed on prod (`Database migrated and seeded`)
- [ ] Create Cloudflare R2 bucket; capture endpoint/keys/public URL (§9.5) — **skipped by user for now**; storage falls back to local disk (ephemeral on Render — uploaded images won't persist across redeploys). Add R2 env vars later to enable.
- [x] Create Render PostgreSQL + Web Service; set §9.4 env vars — **done & live**
- [x] README with click-by-click deploy steps (§9) — `README.md` + `API-ГАЙД.md` (full RU endpoint reference)
- [~] Health-check wired to Render; uptime ping to mitigate idle spin-down (§9.6) — `/health` wired as Render health-check path; **external uptime ping not yet set up** (free tier still cold-starts after ~15 min idle)
- [x] **Milestone:** API live at `https://fastcart-backend.onrender.com`, Swagger at `/swagger` — *verified: /health 200, /products & admin login & dashboard all 200 on prod*

---

## Open items to confirm (TZ §10)
Defaults are already chosen; flip any in one line.
- [ ] D8 tax — charge/display or drop? (default: rate 0, no tax line)
- [ ] D4 reviews — all customers vs verified purchasers? (default: all)
- [ ] D5 currency — confirm single USD
- [ ] D6 condition/features — condition enum in, features deferred — OK?
- [ ] D11 Google sign-in & saved payment options — stubs vs full?
- [ ] D13 variant matrix — admin stocks a subset (assumed)

## Notes / decisions log
*(Append cross-session decisions and deviations from the TZ here, or keep them in the session log.)*

- **2026-06-24 — Phase 0 complete.** Solution file is `FastCart.slnx` (the `.NET 10` SDK emits the new XML solution format; functionally equivalent to `.sln`).
- **Swagger stack:** `Swashbuckle.AspNetCore 10.2.3` pulls `Microsoft.OpenApi 2.7.5`, which flattened namespaces (`Microsoft.OpenApi`, no `.Models`) and replaced inline `OpenApiReference` with typed `OpenApiSecuritySchemeReference`; `AddSecurityRequirement` now takes a `Func<OpenApiDocument, OpenApiSecurityRequirement>`. Wired accordingly in `Program.cs`.
- **JWT:** bearer scheme + Swagger Authorize button are wired now; token *issuance* is Phase 2. Dev signing key lives in `appsettings.Development.json` (placeholder — real `Jwt__Secret` comes from env on Render).
- **Local run:** `dotnet run --project src/FastCart.Api --launch-profile http` → http://localhost:5046 (`/health`, `/swagger`). HTTPS-redirect warning under the http profile is expected. Verified: `/health` → 200 envelope; `/swagger` → 200; `swagger.json` carries the Bearer security scheme.
- **Package versions:** Npgsql.EFCore 10.0.2 · Identity.EFCore 10.0.9 · JwtBearer 10.0.9 · Swashbuckle 10.2.3 · FluentValidation 12.1.1 · AWSSDK.S3 4.0.25.3.
- **2026-06-24 — local DB stood up.** PostgreSQL 18.4 via scoop (user-space, no admin) at `~/scoop/apps/postgresql`, `localhost:5432`, db `fastcart`, `postgres`/`postgres`. Not a service — start each session with `pg_ctl … start`; `dotnet run` then auto-migrates + seeds.
- **2026-06-24 — Phases 1–4 complete.** Review fixes applied: enveloped 401/403, image validation, variant-combination uniqueness, JWT secret fail-fast (all verified live).
- **2026-06-24 — Phase 5 complete.** Checkout/orders/payments/returns shipped & verified live. Payment behind `IPaymentProvider` + resolver (Manual/Bank record-and-hold, CashOnDelivery). Checkout is one EF transaction (no Npgsql retry configured, so manual txns are safe). Order numbers = `#` + random 6-digit with uniqueness retry. Shared logic in `Infrastructure/Orders/OrderingHelpers.cs`.
  - **Deviation:** admin **offline orders skip coupons** — `CouponRedemption.UserId` is a non-null `RESTRICT` FK and offline orders have `UserId == null`. Revisit if offline discounts are needed (add an optional manual discount field).
  - **Tax:** still 0 (D8); checkout honors a non-zero `Tax:Rate` config over `IsTaxable` lines if ever set.
- **2026-06-24 — Phase 6 complete.** CMS/content + newsletter + contact + admin users/roles shipped & verified live (§6.12–6.14). Entities already existed from Phase 1 — only DTOs/services/controllers added (`Application/{Content,Communications,AdminUsers}`, `Infrastructure/{Content,Communications}` + `Identity/AdminUserService`, `Api/Controllers/{ContentControllers,CommunicationsControllers,AdminUsersController}`). No migration needed.
  - **Delete-user FK fix (was carried-forward):** verified delete-behavior — Order.User is **SetNull** (orders kept), CouponRedemption.User & ReturnRequest.User are **Restrict** (block), the rest cascade. Service pre-checks redemptions/returns → 409 with specific message; guards self-delete (422) and self Admin-role-removal (422). Role assign/remove are idempotent; newsletter subscribe idempotent + lowercased.
- **Carried-forward issues (still open):**
  - Minor: `Storage:R2:PublicBaseUrl` hardcoded to `:5046` in dev (slider/banner imageUrls show `:5046` even when run on another port); reset-password returns 422 for unknown email.
- **2026-06-24/25 — Phase 7 complete.** Admin dashboard (§6.15): `Application/Dashboard/DashboardContracts.cs`, `Infrastructure/Dashboard/DashboardService.cs`, `Api/Controllers/AdminDashboardController.cs`; registered in DI. No migration. **Decision:** Sales/Cost/Profit exclude only `Cancelled`/`Returned` (in-progress orders book as sales). `summary.sales` = gross from line snapshots (`UnitPrice×Qty`); `revenue.total` = `Order.Total` (net of discount) — they legitimately differ. Two EF bugs found+fixed live: (1) can't `OrderBy` projected record props after a `GroupBy` → order the groupings by aggregates *then* project; (2) query-string dates arrive `Kind=Unspecified` → Npgsql `timestamptz` reject.
- **2026-06-24/25 — Phase 8 core hardening complete (polish deferred).** All verified live: **auth rate limiting** (built-in fixed-window, 10/60s per IP, enveloped 429 + `Retry-After`, `[EnableRateLimiting("auth")]` on `AuthController`); **secure-by-default authz** (global `FallbackPolicy=RequireAuthenticatedUser`; `[AllowAnonymous]` added to `HealthController`); **global UTC `DateTime` JSON converter** (`Api/Common/UtcDateTimeJsonConverter.cs`) root-fixing the `Unspecified→timestamptz` bug class for all body dates; **query-string date UTC** via shared `Application/Common/DateTimeExtensions.ToUtc()` (used in `AdminOrderService` + `DashboardService`); **InvariantCulture** money in coupon message (`CouponService:46`); **refresh-token pruning** (`ExecuteDeleteAsync` of dead tokens in `AuthService.IssueTokensAsync`); **structured logging** (`AddHttpLogging` method/path/status/duration + JSON console in Production, EF SQL command logs quieted to Warning). Carried-forward Phase-8 backlog (rate-limit, token-prune, money-culture) now all resolved.
  - **Deferred polish:** full FluentValidation migration (DataAnnotations already satisfies §8 "all bodies validated"); Swagger request/response examples; dedicated deep N+1/perf audit.
- **2026-06-24/25 — Phase 9 prep done (live deploy needs user).** `Dockerfile` (multi-stage SDK10→aspnet10, `$PORT` bind), `.dockerignore`, `README.md` (deploy guide + §9.4 env table). Migrate-on-deploy already wired. **Docker image build NOT verifiable locally** (Docker not installed) — validated instead via `dotnet publish -c Release` + a **Production dry-run of the published DLL** (boots Production w/ JWT-secret fail-fast, binds `$PORT`, `/health` 200, JSON logs, migrate-on-startup ran). Remaining: create R2 bucket + Render PG/Web Service + env vars, then deploy.
- **2026-06-25 — DEPLOYED & LIVE 🎉** `https://fastcart-backend.onrender.com` (Swagger at `/swagger`). Render Web Service (Docker) + Render PostgreSQL. **git repo created this session** (3 commits) + pushed to GitHub `https://github.com/Us-user/FastCart-Backend`. Seeded admin: `admin@gmail.com` / `Abuumar5` (verified login → JWT w/ Admin role → `/admin/dashboard/summary` 200). DB empty (no catalog seeded yet).
  - **Two deploy fixes made & shipped:** (1) `DependencyInjection.BuildConnectionString` auto-converts `postgres(ql)://` URLs → Npgsql key-value + SSL (Render/Heroku/Railway give URL form); (2) `DependencyInjection.ResolveRawConnectionString` — reads `ConnectionStrings:DefaultConnection`, falls back to conventional `DATABASE_URL` env var (used by both DbContext + startup migrate check).
  - **Deploy gotchas hit (for next time):** (a) `ConnectionStrings__DefaultConnection` env var wasn't picked up → switched to `DATABASE_URL`; (b) Render **Internal** DB URL (bare host `dpg-xxx-a`) failed → **External Database URL** (`...-a.oregon-postgres.render.com`) worked. Likely region mismatch internal-vs-external.
  - **R2 skipped** — running on local disk storage (ephemeral on Render; image uploads won't survive redeploys). Add `Storage__R2__*` env vars to enable persistent images.
  - **`API-ГАЙД.md`** added — full Russian endpoint reference (all routes, access levels, examples, error codes).
