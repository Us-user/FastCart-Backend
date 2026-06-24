# FastCart — Backend Technical Specification

**Project:** FastCart online store — backend API
**Version:** 1.2 (supersedes v1.1)
**Date:** 24 June 2026
**Status:** Specification for a new, ground-up backend. The previous `store-api.softclub.tj` API is treated as a reference for patterns only, not a foundation.

**Changes from v1.1:**
- **Full product variants.** Each combination of options (Size × Colour × Weight …) is its own SKU with its own price, discount, cost, and stock. Modeled flexibly so new option axes never require a schema change.
- **Image storage locked to object storage (Cloudflare R2, S3-compatible).** The database stores only URLs. Resolves the Render ephemeral-disk issue.

---

## 1. Overview

FastCart is a **single-store** online shop (one operating business, not a multi-vendor marketplace). This document specifies the **backend API only**. The storefront and admin panel are separate frontend projects built later against this API.

A **single unified backend** serves both audiences — there are not two APIs. The same service exposes **public/customer** endpoints (browse, cart, wishlist, checkout, orders, reviews, profile) and **admin** endpoints (catalog, orders, coupons, banners, users, dashboard), separated by **role-based authorization**. A customer holds the `Customer` role; an operator holds the `Admin` role.

The design is derived from the storefront screens, the admin screens, and the reference API. Where the screens and the reference API disagreed, the screens won.

---

## 2. Scope

### 2.1 In scope

Authentication and accounts; product catalog (categories, subcategories, brands, colors, tags, **products with option-based variants** and images); browsing with filtering, sorting, and pagination; cart; wishlist; reviews and ratings; coupons; checkout; the full order lifecycle including a test/mock payment flow; addresses; returns and cancellations (lightweight); home-page CMS content (sliders and promo banners with countdowns); newsletter signup; contact messages; admin user/role management; admin dashboard reporting; and deployment to Render with PostgreSQL and Cloudflare R2 (§9).

### 2.2 Out of scope (future work)

- **Real payment gateway integration.** Handled by a swappable provider abstraction with a test/manual implementation now (§7.3). A real or regional (e.g., Tajikistan) gateway is a later swap.
- **Multi-vendor / marketplace.** Single store only.
- **Multi-currency.** Single configurable currency (Decision D5).
- **"Features" facet** (e.g., "8GB Ram", "Metallic") and rich product specs — shown as a storefront filter but with no admin management; deferred (Decision D6).
- **About-page marketing content** (company stats, team members) — static frontend content, not backend data.

---

## 3. Decisions & Assumptions

Each can be overridden; the rest of the spec assumes them.

| # | Decision | Rationale | How to change |
|---|----------|-----------|---------------|
| **D1** | **Full per-combination variants.** Price, discount, cost, stock, and SKU live on the **variant**, not the product. A variant is one combination of the product's option values (e.g., *Size: M / Colour: Red*). Modeled with option types + values + variants, so any number of axes works without schema changes. | Your choice (option b): each size/colour/etc. priced and stocked independently. | Collapse back to product-level pricing (simpler, but loses per-combination stock/price). |
| **D2** | **Payment is "record and hold."** At checkout the buyer may submit any payment data. The order and payment are created with status **Pending**; an admin later marks **Paid** (or a mock endpoint flips it). All payment logic sits behind one provider interface. | Your instruction: accept any data, status pending. Mirrors the admin Paid/Pending columns. | Implement a real provider against the same interface. |
| **D3** | **No sensitive card data stored.** The API accepts payment intent but never persists card numbers or CVV — only the method and a (mock) reference. | Security/PCI good practice; free to keep. | N/A — keep regardless. |
| **D4** | **Reviews** may be posted by any authenticated customer; admins can delete any review. | "Post freely." Simple. | Restrict to verified purchasers, or add moderation states. |
| **D5** | **Single currency**, configurable, default **USD**. Stored on each order. | Storefront shows `$`. | Add multi-currency later. |
| **D6** | **Condition** is a product enum (`BrandNew` / `Refurbished` / `Old`) and filterable. **Features** filter deferred (heterogeneous, no admin UI). | Condition is a clean small set; Features is not. | Add a Features/attribute model with admin management. |
| **D7** | **Shipping** is a free flat rate (amount `0`). | Storefront checkout shows "Shipping: Free". | Add shipping methods/rates. |
| **D8** | **Tax**: each product carries an `IsTaxable` flag; the system has one configurable tax rate. Tax defaults to `0`. The storefront showed no tax line — confirm whether to charge/display it. | The Add-Product screen has an "Add tax for this product" toggle. | Set a non-zero rate and surface a tax line, or drop tax. |
| **D9** | **Cost is per variant** and is **snapshotted onto each order line** (`UnitCost`) at purchase time, so dashboard profit stays correct even if costs change later. | The dashboard reports Cost/Profit; per-line snapshots make history stable. | Compute profit differently. |
| **D10** | **Stack: ASP.NET Core (.NET) + EF Core + PostgreSQL**, ASP.NET Identity for auth. **Deployed on Render (free tier)** with Render-managed PostgreSQL and **Cloudflare R2** for images. | Matches reference API (.NET) and your toolchain; PostgreSQL + Render + R2 chosen for free, low-hassle hosting (§9). | Tell me a different stack/host and I'll re-spec conventions and §9. |
| **D11** | **Google sign-in** and **"My Payment Options"** are minimal/optional stubs for now. | Peripheral to a test backend. | Promote either to a full feature. |
| **D12** | **Images in object storage (Cloudflare R2), DB stores URLs only.** Local disk only for local dev. | Databases shouldn't serve binary files; R2 is free, S3-compatible, no egress fees, and survives redeploys. | Swap to AWS S3 / Backblaze B2 (config change, same code). |
| **D13** | **Variants need not cover the full matrix.** The admin defines option axes and values, then enables/prices only the combinations actually sold; unstocked combinations simply don't exist as variants. | Real stores rarely stock every combination. | Auto-require every combination. |

---

## 4. Architecture

### 4.1 Style and layering

Layered (clean-architecture-style) ASP.NET Core solution, consistent with the reference API's `Domain.Dtos.*` structure: **API** (controllers, request/response models, validation, auth filters, Swagger); **Application** (services, business rules — checkout, pricing, coupons, stock — DTOs, the payment-provider abstraction, the storage abstraction); **Domain** (entities and enums); **Infrastructure** (EF Core `DbContext`, repositories, Identity, R2/storage client, email sender, payment providers).

### 4.2 Technology

- **.NET / ASP.NET Core** Web API (current LTS).
- **Entity Framework Core** with **PostgreSQL** via the **Npgsql** provider.
- **ASP.NET Identity** for users, password hashing, and reset tokens.
- **JWT** bearer auth with refresh tokens.
- **Cloudflare R2** (S3-compatible) for image storage, via the AWS S3 SDK.
- **Swagger / OpenAPI** with documented request/response schemas (an improvement over the reference API's bare "200 Success"); served at `/swagger`, available the moment the app runs.
- **FluentValidation** (or DataAnnotations) for validation.
- **Docker** — ships with a `Dockerfile` for reproducible builds and Render deployment.

### 4.3 API conventions

Corrects three weaknesses found in the reference API (verbs-in-path naming, write payloads in query strings, undocumented responses).

- **Base URL:** `/api/v1`, versioned via the URL segment.
- **RESTful resources:** nouns + HTTP verbs, e.g. `GET/POST /api/v1/products`, `GET/PUT/DELETE /api/v1/products/{id}` — *not* `/Product/get-products`.
- **Request bodies:** `application/json` for data; `multipart/form-data` only where files are uploaded. **Write payloads are never in query strings.**
- **Response envelope:**

  ```json
  { "success": true, "message": "…", "data": { }, "errors": null }
  ```

  On failure: `success: false`, `data: null`, `errors` carries field-level messages.
- **Pagination:** `pageNumber` (default 1), `pageSize` (default 20, max 100); responses include `items`, `pageNumber`, `pageSize`, `totalCount`, `totalPages`.
- **Filtering & sorting:** query parameters (reads, so query strings are appropriate). Full product filter set in §6.5.
- **Errors:** `400` validation, `401` unauthenticated, `403` wrong role, `404` not found, `409` conflict (out of stock, duplicate SKU), `422` business-rule failure.
- **Dates:** UTC ISO 8601. **Money:** decimal. **IDs:** `int` for catalog/commerce entities, `string` GUID for users (Identity). Orders carry a human-friendly `OrderNumber`.

### 4.4 Authentication and authorization

- **Roles:** `Customer` (default on registration) and `Admin`, seeded at startup with an initial admin account.
- **Tokens:** short-lived JWT **access token** (≈15–60 min) in `Authorization: Bearer <token>`, plus a long-lived, rotating, revocable **refresh token**.
- **Per-endpoint authorization:** marked **Public / Customer / Admin** below. Customers reach only their own resources; admins have full access.
- **Password reset:** Identity email-token flow; `forgot-password` always returns `200` (anti-enumeration).

---

## 5. Data Model

Audit fields (`CreatedAt`, `UpdatedAt`, UTC) are implied on all entities. All tables, keys, and indexes are created by **EF Core migrations** (§9.2).

### 5.1 Identity & users

**User** (ASP.NET Identity, PK `string`/GUID): `Id`, `UserName` (unique), `Email` (unique), `PhoneNumber`, `PasswordHash`, `EmailConfirmed`. Login accepts email *or* phone.
**UserProfile** (1:1 with User): `UserId` (FK), `FirstName`, `LastName`, `Dob?`, `ImageUrl?`.
**Role**: `Id`, `Name` (`Customer`, `Admin`).
**RefreshToken**: `Id`, `UserId`, `Token`, `ExpiresAt`, `RevokedAt?`.
**Address**: `Id`, `UserId` (FK), `FirstName`, `LastName`, `StreetAddress`, `Apartment?`, `City`, `PhoneNumber`, `Email`, `IsDefault`.

### 5.2 Catalog

**Category** — `Id`, `Name`, `ImageUrl`.
**SubCategory** — `Id`, `CategoryId` (FK), `Name`. (Category → SubCategory hierarchy.)
**Brand** — `Id`, `Name`.
**Color** — `Id`, `Name`, `HexCode` (admin color modal captures hex).
**Tag** — `Id`, `Name`.

**Product** — shared attributes only; **all money and stock live on variants**:

| Field | Type | Notes |
|-------|------|-------|
| Id | int | |
| Name | string | max 100 |
| Code | string | optional product-level handle/code |
| Description | string (HTML) | rich text |
| SubCategoryId | int | FK → SubCategory (Category derived via it) |
| BrandId | int | FK → Brand |
| IsTaxable | bool | "Add tax for this product" (D8) |
| Condition | enum | `BrandNew` / `Refurbished` / `Old`, nullable (D6) |
| CreatedAt | datetime | drives "New Arrival" / NEW badge |

Related to Product:

- **ProductImage** — `Id`, `ProductId`, `Url` (R2 object URL), `IsPrimary`, `SortOrder`.
- **ProductTag** (join) — `ProductId`, `TagId`.

**Variant model (the core of v1.2):**

**ProductOption** — an option axis for a product.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | |
| ProductId | int | FK |
| Name | string | e.g. "Size", "Colour", "Weight" |
| SortOrder | int | display order |

**ProductOptionValue** — a value within an axis.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | |
| ProductOptionId | int | FK → ProductOption |
| Value | string | e.g. "M", "Red", "10kg" |
| ColorId | int | nullable FK → Color (for colour swatch/hex) |
| SortOrder | int | |

**ProductVariant** — one sellable combination; **carries the money and stock**.

| Field | Type | Notes |
|-------|------|-------|
| Id | int | |
| ProductId | int | FK |
| Sku | string | unique |
| Price | decimal | list price for this variant |
| HasDiscount | bool | |
| DiscountPrice | decimal | nullable; **effective price** = `DiscountPrice` if `HasDiscount` else `Price` |
| CostPrice | decimal | for profit (D9) |
| StockCount | int | per-variant stock |
| IsActive | bool | hide a variant without deleting |

**ProductVariantOptionValue** (join) — links a variant to exactly one value per option axis.

| Field | Type | Notes |
|-------|------|-------|
| ProductVariantId | int | FK |
| ProductOptionValueId | int | FK |

> A variant references one `ProductOptionValue` per `ProductOption` of its product. The combination of option values is **unique per product**. Not every possible combination need exist (D13).

**Derived/display values** (computed, not stored): a product's **`fromPrice`** = the minimum effective price over its active variants; **price range** = min–max effective price; **in stock** = any active variant with `StockCount > 0`; **colour swatches** on a card = the colour option values present across its variants.

### 5.3 Cart & wishlist

**Cart** — `Id`, `UserId` (one active cart per user).
**CartItem** — references the **variant**:

| Field | Type | Notes |
|-------|------|-------|
| Id | int | |
| CartId | int | FK |
| ProductVariantId | int | FK → ProductVariant (the chosen combination) |
| Quantity | int | |

**WishlistItem** — `Id`, `UserId`, `ProductId`, `CreatedAt` (unique per user+product). The wishlist is product-level (the heart on a product card); variant is chosen later at add-to-cart.

### 5.4 Coupons

**Coupon** — `Id`, `Code` (unique), `DiscountType` (`Percentage` / `FixedAmount`), `DiscountValue`, `MinOrderAmount?`, `MaxDiscountAmount?` (caps a percentage), `StartsAt?`, `ExpiresAt?`, `UsageLimit?`, `PerUserLimit?`, `TimesUsed`, `IsActive`.
**CouponRedemption** — `Id`, `CouponId`, `UserId`, `OrderId`, `UsedAt` (enforces per-user limits).

### 5.5 Orders & payments

**Order**

| Field | Type | Notes |
|-------|------|-------|
| Id | int | |
| OrderNumber | string | unique, human-friendly (e.g. `#125128`) |
| UserId | string | nullable — null for admin "offline" orders |
| CustomerName / CustomerEmail | string | snapshot |
| Status | enum | `New` / `Ready` / `Shipped` / `Received` / `Cancelled` / `Returned` |
| PaymentStatus | enum | `Pending` / `Paid` / `Failed` / `Refunded` |
| PaymentMethod | enum | `CashOnDelivery` / `Bank` |
| Currency | string | default USD |
| Subtotal | decimal | sum of line totals |
| DiscountAmount | decimal | coupon discount |
| CouponCode | string | nullable snapshot |
| TaxAmount | decimal | usually 0 (D8) |
| ShippingAmount | decimal | 0 / free (D7) |
| Total | decimal | `Subtotal − Discount + Tax + Shipping` |
| CustomerNote | string | nullable |
| Ship* / Bill* fields | — | address snapshots (FirstName, LastName, Street, Apartment, City, Phone, Email) |
| CancelledAt? / CancelReason? | datetime / string | |

**OrderItem** — snapshots so later catalog/variant edits never rewrite history:

| Field | Type | Notes |
|-------|------|-------|
| Id | int | |
| OrderId | int | FK |
| ProductId | int | nullable snapshot ref |
| ProductVariantId | int | nullable snapshot ref |
| ProductName | string | snapshot |
| Sku | string | snapshot (variant SKU) |
| VariantDescription | string | snapshot, e.g. "Size: M / Colour: Red" |
| UnitPrice | decimal | snapshot at purchase time |
| UnitCost | decimal | snapshot for profit (D9) |
| Quantity | int | |
| LineTotal | decimal | `UnitPrice × Quantity` |

**Payment** — `Id`, `OrderId` (FK), `Method` (`CashOnDelivery` / `Bank`), `Provider` (e.g. `Manual` / `Test`), `Amount`, `Currency`, `Status` (`Pending` / `Paid` / `Failed` / `Refunded`), `Reference?`, `PaidAt?`.

> **No card PAN or CVV is ever stored** (D3). At most a non-sensitive note or last-4 if supplied.

**ReturnRequest** — `Id`, `OrderId`, `UserId`, `Reason`, `Status` (`Requested` / `Approved` / `Rejected` / `Completed`), `ResolvedAt?`.

### 5.6 Content & misc

**Slider** (home main carousel) — `Id`, `ImageUrl`, `Subtitle`, `Title`, `SortOrder`, `IsActive`.
**Banner** (promo with countdown — backs Flash Sales) — `Id`, `ImageUrl`, `Title`, `CategoryId?` (FK), `EndsAt` (countdown target), `IsActive`.
**NewsletterSubscriber** — `Id`, `Email` (unique).
**ContactMessage** — `Id`, `Name`, `Email`, `Phone`, `Message`, `IsRead`.

### 5.7 Enumerations

- **OrderStatus:** `New`, `Ready`, `Shipped`, `Received`, `Cancelled`, `Returned`
- **PaymentStatus:** `Pending`, `Paid`, `Failed`, `Refunded`
- **PaymentMethod:** `CashOnDelivery`, `Bank`
- **DiscountType:** `Percentage`, `FixedAmount`
- **ProductCondition:** `BrandNew`, `Refurbished`, `Old`
- **ReturnStatus:** `Requested`, `Approved`, `Rejected`, `Completed`
- **Role:** `Customer`, `Admin`

---

## 6. API Reference

All paths under `/api/v1`. **Auth:** Public / Customer / Admin.

### 6.1 Authentication — `/auth`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/auth/register` | Public | Register a customer: `userName`, `email`, `phoneNumber`, `password`, `confirmPassword`. |
| POST | `/auth/login` | Public | Login with **email or phone** + password → access + refresh tokens. |
| POST | `/auth/refresh` | Public | Exchange a refresh token for new tokens. |
| POST | `/auth/logout` | Customer | Revoke the current refresh token. |
| POST | `/auth/forgot-password` | Public | Email a reset token. Always 200. |
| POST | `/auth/reset-password` | Public | `token`, `newPassword`, `confirmPassword`. |
| POST | `/auth/change-password` | Customer | `currentPassword`, `newPassword`, `confirmPassword`. |
| GET | `/auth/me` | Customer | Current user + profile + roles. |
| POST | `/auth/external/google` | Public | *(Optional, D11)* Google OAuth sign-in. |

### 6.2 Profile — `/profile`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/profile` | Customer | Get own profile. |
| PUT | `/profile` | Customer | Update. Multipart: `image`, `firstName`, `lastName`, `email`, `phoneNumber`, `dob`. |

### 6.3 Addresses — `/addresses`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/addresses` | Customer | List own (Address Book). |
| POST | `/addresses` | Customer | Add. |
| PUT | `/addresses/{id}` | Customer | Update. |
| DELETE | `/addresses/{id}` | Customer | Delete. |
| PUT | `/addresses/{id}/default` | Customer | Set default. |

### 6.4 Catalog — categories, subcategories, brands, colors, tags

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/categories` | Public | List (optionally with subcategories). |
| GET | `/categories/{id}` | Public | Detail. |
| POST | `/categories` | Admin | Create. Multipart: `name`, `image`. |
| PUT | `/categories/{id}` | Admin | Update (multipart). |
| DELETE | `/categories/{id}` | Admin | Delete. |
| GET | `/subcategories?categoryId=` | Public | List, optionally by category. |
| GET | `/subcategories/{id}` | Public | Detail. |
| POST/PUT/DELETE | `/subcategories[/{id}]` | Admin | Manage (`categoryId`, `name`). |
| GET | `/brands?brandName=&pageNumber=&pageSize=` | Public | List/search. |
| GET | `/brands/{id}` | Public | Detail. |
| POST/PUT/DELETE | `/brands[/{id}]` | Admin | Manage (`name`). |
| GET | `/colors?colorName=&pageNumber=&pageSize=` | Public | List/search. |
| GET | `/colors/{id}` | Public | Detail. |
| POST/PUT/DELETE | `/colors[/{id}]` | Admin | Manage (`name`, `hexCode`). |
| GET | `/tags` | Public | List. |
| POST/PUT/DELETE | `/tags[/{id}]` | Admin | Manage. |

### 6.5 Products & variants — `/products`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/products` | Public | List with filtering/sorting/pagination (below). Cards include `fromPrice`, price range, `inStock`, colour swatches, avg rating. |
| GET | `/products/{id}` | Public | Full detail: images, description, brand, category, **option axes + values (with colour swatches), and the variant list** (each: `id`, `sku`, its option-value combo, `price`, effective price, `stockCount`, `isActive`), plus rating summary. The frontend resolves a buyer's option selection to one variant. |
| GET | `/products/{id}/related` | Public | Related items (same subcategory). |
| POST | `/products` | Admin | Create product **with options, values, and variants** in one call. Multipart: `name`, `code?`, `description`, `subCategoryId`, `brandId`, `isTaxable`, `condition`, `tagIds[]`, `images[]`; `options[]` = `{ name, sortOrder, values: [{ value, colorId?, sortOrder }] }`; `variants[]` = `{ sku, optionValues: [{ optionName, value }…], price, hasDiscount, discountPrice?, costPrice, count, isActive }`. Each variant lists one value per option. |
| PUT | `/products/{id}` | Admin | Update product-level fields (JSON or multipart). |
| DELETE | `/products/{id}` | Admin | Delete (with its options/variants/images). |
| POST | `/products/bulk-delete` | Admin | `ids[]` (bulk-delete modal). |
| POST | `/products/{id}/images` | Admin | Add images (multipart → R2). |
| DELETE | `/products/{id}/images/{imageId}` | Admin | Remove an image. |
| GET | `/products/{id}/variants` | Admin | List variants. |
| POST | `/products/{id}/variants` | Admin | Add a variant (`sku`, option values, price, discount, cost, count, isActive). |
| PUT | `/products/{id}/variants/{variantId}` | Admin | Update a variant. |
| DELETE | `/products/{id}/variants/{variantId}` | Admin | Delete a variant. |
| PUT | `/products/{id}/variants/{variantId}/stock` | Admin | Quick stock update (`count`). |
| POST/PUT/DELETE | `/products/{id}/options[/{optionId}]` | Admin | Manage option axes/values (e.g., add a "Material" axis later — no schema change, D1/D13). |

**Product list filters (query params):** `q` (name), `categoryId`, `subCategoryId`, `brandIds[]`, `colorIds[]`, `tagIds[]`, `minPrice`, `maxPrice`, `condition`, `minRating`, `hasDiscount`, `isNew`, `inStock`, `sort` (`newest` | `price_asc` | `price_desc` | `popularity` | `rating`), `pageNumber`, `pageSize`.

- **Variant-aware filtering:** `minPrice`/`maxPrice` match a product if **any** variant's effective price falls in range; `colorIds` match products having a variant whose colour option value is selected; `price_asc`/`price_desc` sort by the product's `fromPrice`; `hasDiscount` = any variant discounted; `inStock` = any active variant with stock. **Best Selling** = `sort=popularity` (units sold); **New Arrival** / NEW badge = `sort=newest` / `isNew` from `CreatedAt`.

### 6.6 Reviews

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/products/{id}/reviews?pageNumber=&pageSize=` | Public | List + rating summary. |
| POST | `/products/{id}/reviews` | Customer | Create: `rating` (1–5), `comment` (D4). |
| DELETE | `/reviews/{id}` | Admin or owner | Delete / moderate. |

### 6.7 Cart — `/cart` (operates on variants)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/cart` | Customer | Current cart with items (product + variant details) and computed totals. |
| POST | `/cart/items` | Customer | Add: `productVariantId`, `quantity`. |
| PUT | `/cart/items/{variantId}` | Customer | Set quantity (Update Cart). |
| POST | `/cart/items/{variantId}/increment` | Customer | Stepper +. |
| POST | `/cart/items/{variantId}/decrement` | Customer | Stepper −. |
| DELETE | `/cart/items/{variantId}` | Customer | Remove a line. |
| DELETE | `/cart` | Customer | Clear (Remove all). |

### 6.8 Wishlist — `/wishlist`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/wishlist` | Customer | List wishlist items (product-level). |
| POST | `/wishlist/{productId}` | Customer | Add. |
| DELETE | `/wishlist/{productId}` | Customer | Remove. |
| POST | `/wishlist/move-all-to-cart` | Customer | "Move All To Bag" — adds the default/only variant of each item (items needing an option choice are flagged for the UI). |

### 6.9 Coupons

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/coupons/validate` | Customer | `code`, `cartTotal` → validity + computed discount (cart/checkout "Apply"). |
| GET | `/admin/coupons` | Admin | List. |
| GET | `/admin/coupons/{id}` | Admin | Detail. |
| POST/PUT/DELETE | `/admin/coupons[/{id}]` | Admin | Create / update / delete. |

### 6.10 Checkout & customer orders — `/orders`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/orders/checkout` | Customer | Place an order from the cart (§7.1). |
| GET | `/orders?status=` | Customer | List own (My Orders; `status=Cancelled` → My Cancellations). |
| GET | `/orders/{id}` | Customer | Own order detail. |
| POST | `/orders/{id}/cancel` | Customer | Cancel if `New` or `Ready`. |
| POST | `/orders/{id}/return` | Customer | Request a return (after `Received`). |
| GET | `/returns` | Customer | Own return requests (My Returns). |
| POST | `/orders/{id}/pay` | Customer | Record payment for an existing order; sets payment `Pending`. |

### 6.11 Admin orders — `/admin/orders`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/admin/orders?status=&paymentStatus=&q=&from=&to=&sort=&pageNumber=&pageSize=` | Admin | All orders, filtered/sorted/paged. |
| GET | `/admin/orders/{id}` | Admin | Detail. |
| POST | `/admin/orders` | Admin | Create a manual/offline order ("Add order"); references variants. |
| PUT | `/admin/orders/{id}/status` | Admin | Set fulfillment status. |
| PUT | `/admin/orders/{id}/payment-status` | Admin | Mark `Paid` / `Refunded`. |
| GET | `/admin/returns` | Admin | List return requests. |
| PUT | `/admin/returns/{id}` | Admin | Approve / reject / complete. |

### 6.12 Content / CMS

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/sliders` | Public | Active home main sliders. |
| GET | `/banners` | Public | Active promo banners (category + countdown `endsAt`) — backs Flash Sales. |
| GET/POST/PUT/DELETE | `/admin/sliders[/{id}]` | Admin | Manage sliders (image, subtitle, title, order). |
| GET/POST/PUT/DELETE | `/admin/banners[/{id}]` | Admin | Manage banners (image, categoryId, title, endsAt). |

### 6.13 Newsletter & contact

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/newsletter/subscribe` | Public | `email`. |
| GET | `/admin/newsletter` | Admin | List subscribers. |
| POST | `/contact` | Public | `name`, `email`, `phone`, `message`. |
| GET | `/admin/contact-messages` | Admin | List messages. |

### 6.14 Admin users & roles — `/admin/users`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/admin/users?userName=&pageNumber=&pageSize=` | Admin | List. |
| GET | `/admin/users/{id}` | Admin | Detail. |
| DELETE | `/admin/users/{id}` | Admin | Delete. |
| POST | `/admin/users/{id}/roles` | Admin | Assign role (`roleId`). |
| DELETE | `/admin/users/{id}/roles/{roleId}` | Admin | Remove role. |
| GET | `/admin/roles` | Admin | List roles. |

### 6.15 Admin dashboard — `/admin/dashboard`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/admin/dashboard/summary?from=&to=` | Admin | Sales, Cost, Profit (uses order-line `UnitPrice`/`UnitCost` snapshots). |
| GET | `/admin/dashboard/revenue?year=` | Admin | Monthly revenue + order counts. |
| GET | `/admin/dashboard/top-products?metric=sales|units&take=` | Admin | Top selling / top by units. |
| GET | `/admin/dashboard/recent-transactions?take=` | Admin | Recent transactions. |

### 6.16 System

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/health` | Public | Liveness/readiness for Render and uptime pings (§9.6). |

---

## 7. Key Workflows

### 7.1 Checkout

`POST /orders/checkout` body: `shippingAddress` (inline or `addressId`), optional `billingAddress`, `saveAddress` (bool), `paymentMethod` (`CashOnDelivery` | `Bank`), optional `paymentDetails`, optional `couponCode`, optional `customerNote`.

Atomic steps (single transaction):

1. Load the user's cart; reject if empty.
2. For each line: confirm the **variant** exists, is active, and `StockCount ≥ quantity`. Reject with `409` otherwise.
3. **Recompute prices server-side.** Unit price = variant effective price (`DiscountPrice` if `HasDiscount`, else `Price`). `Subtotal` = Σ line totals.
4. If `couponCode` present, validate (active, in-window, meets `MinOrderAmount`, within usage limits) and compute `DiscountAmount`.
5. `TaxAmount` (0 unless taxable products + non-zero configured rate); `ShippingAmount` (0, free).
6. `Total = Subtotal − DiscountAmount + TaxAmount + ShippingAmount`.
7. Create the **Order** (`New`, payment `Pending`) with **OrderItem snapshots**: product name, variant SKU, `VariantDescription` (e.g. "Size: M / Colour: Red"), `UnitPrice`, **`UnitCost`** (from the variant, for profit), quantity.
8. If `saveAddress`, persist to the Address Book.
9. Create the **Payment** record (§7.3): status `Pending`.
10. **Decrement the variant's stock** by the ordered quantity.
11. If a coupon was used, increment `TimesUsed` and write a `CouponRedemption`.
12. **Clear the cart.**
13. Return the order; message conveys it is placed and **pending** payment/confirmation.

### 7.2 Order status lifecycle

```
New ──► Ready ──► Shipped ──► Received
  │        │
  └────────┴──► Cancelled            (customer while New/Ready, or admin)
Received ──► Returned                (via an approved ReturnRequest)
```

Payment status is independent: `Pending → Paid` (admin or mock), `Paid → Refunded` (on completed return), or `→ Failed`. Customer **My Orders** = own orders; **My Cancellations** = `status=Cancelled`; **My Returns** = own `ReturnRequest`s.

### 7.3 Payment handling

All payment logic sits behind an **`IPaymentProvider`** abstraction (D2), so a real gateway is a later swap with no change to order logic.

- **Cash on delivery:** real, no integration. Order created, payment `Pending`; admin marks `Paid` after delivery.
- **Bank (test):** buyer may submit any data. A **Manual/Test provider** records the payment as `Pending` and returns success-of-recording (a config toggle can auto-mark `Paid` or simulate `Failed` for testing both paths). **No card data persisted** (D3).
- **Admin** flips payment status via `PUT /admin/orders/{id}/payment-status`.
- **Future:** implement `IPaymentProvider` against a real/regional gateway + a webhook endpoint.

### 7.4 Stock rules (per variant)

Variant stock decremented at order creation (step 10). On **Cancelled** or **completed Returned**, the variant's stock is **restored**. A variant at `StockCount = 0` is unavailable; a product with no in-stock active variant shows out of stock (card) and disables that option on the detail page.

### 7.5 Coupon rules

Applies only if active, in its date window, the cart meets `MinOrderAmount`, and total/per-user limits aren't exceeded. `Percentage` discounts respect `MaxDiscountAmount`. Recomputed authoritatively at checkout, never taken from the client.

---

## 8. Cross-cutting & Non-functional

- **Security:** Identity password hashing; short-lived JWT + rotating, revocable refresh tokens; HTTPS only; role-based authorization on every non-public endpoint; ownership checks; **no raw card data stored**; rate limiting on auth endpoints; anti-enumeration on forgot-password; CORS restricted to the storefront and admin origins.
- **Validation:** all bodies validated. Registration mirrors the reference contract (`userName`, `email`, `phoneNumber`, `password`, `confirmPassword`) with a password policy. Variant `Sku` unique; `Price`/`CostPrice` ≥ 0; `StockCount` ≥ 0; a variant must specify one value per option axis.
- **Image storage:** all uploads go to **Cloudflare R2** via a storage abstraction (D12, §9.3); the DB stores object URLs only. Validate content type and size (admin specifies SVG/JPG/PNG/GIF).
- **Pagination/filtering/sorting & error envelope:** consistent across all list endpoints (§4.3).
- **Currency & dates:** single configurable currency per order (default USD); UTC ISO 8601; decimal money.
- **Seeding:** seed `Admin`/`Customer` roles + an initial admin (credentials from env, §9.4); optionally seed sample catalog with options/variants.
- **API documentation:** Swagger/OpenAPI with full request/response schemas at `/swagger`.
- **Observability:** structured logging; `GET /health` (§6.16).
- **Performance:** index FKs and common filter columns (product name, variant price, category/brand, order date/status); paginate every list; avoid N+1 via EF `Include`/projection; compute product `fromPrice`/in-stock via efficient aggregate queries over variants.

---

## 9. Deployment & Configuration

Hosting doesn't affect the API design; it sets a few startup config values. Target: **Render (free tier)** + **Render-managed PostgreSQL** + **Cloudflare R2** for images. Click-by-click steps ship in the README.

### 9.1 Target environment

- A Render **Web Service** runs the API (Docker build from the repo).
- A Render **PostgreSQL** instance holds the database.
- **Cloudflare R2** holds uploaded images.
- **HTTPS/TLS** is automatic on `*.onrender.com`.

### 9.2 Database & schema creation

- PostgreSQL via the **Npgsql** EF Core provider.
- **The schema is created by EF Core migrations, not by hand** — entities (§5) define the model; `dotnet ef migrations add` + `database update` build every table, key, and index.
- **Migrations apply automatically on deploy** (startup hook or Render pre-deploy command).
- **Seeding** on first run: `Admin`/`Customer` roles + the initial admin (env-supplied credentials).

### 9.3 Image storage — Cloudflare R2 (D12)

- Images are **not** stored in the database (binary blobs bloat backups, exhaust the free DB's connections, and can't be CDN-cached) and **not** on Render's local disk (ephemeral — wiped on every redeploy).
- Uploads go to **Cloudflare R2** (S3-compatible, free tier, no egress fees); the DB stores the **object URL** in `ProductImage.Url`, `Category.ImageUrl`, `Slider.ImageUrl`, `Banner.ImageUrl`, `UserProfile.ImageUrl`.
- Accessed via the **AWS S3 SDK** pointed at R2's endpoint. Switching to AWS S3 or Backblaze B2 later is a **config change, not code**.
- Local disk is used only for local development.

### 9.4 Configuration (environment variables)

All deploy-specific values live in environment variables / `appsettings`, never in code:

| Variable | Purpose |
|----------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection (Render DB internal string) |
| `Jwt__Secret` | access-token signing key |
| `Jwt__Issuer`, `Jwt__Audience` | token issuer / audience |
| `Jwt__AccessTokenMinutes`, `Jwt__RefreshTokenDays` | token lifetimes |
| `Cors__AllowedOrigins` | comma-separated storefront + admin URLs |
| `Seed__AdminEmail`, `Seed__AdminPassword` | initial admin, seeded on first run |
| `Storage__R2__Endpoint` | R2 S3-compatible endpoint |
| `Storage__R2__AccessKeyId`, `Storage__R2__SecretAccessKey` | R2 credentials |
| `Storage__R2__Bucket` | R2 bucket name |
| `Storage__R2__PublicBaseUrl` | public base URL used to build stored image URLs |

### 9.5 Deploy flow (high level)

1. Push the project to a Git repo.
2. Create a **Cloudflare R2** bucket; note its endpoint, keys, and public URL.
3. In Render, create a **PostgreSQL** instance; copy its internal connection string.
4. Create a **Web Service** from the repo (Docker build).
5. Set the environment variables from §9.4.
6. Deploy. Migrations apply; roles + admin seed.
7. API live at `https://<service>.onrender.com`; **Swagger at `…/swagger`**.

### 9.6 Health check

`GET /health` (§6.16) for Render health checks and uptime pings (helps mitigate free-tier idle spin-down).

### 9.7 Free-tier limits (Render)

- The free web service **spins down when idle** and cold-starts on the next request (a short first-hit delay).
- Free PostgreSQL has **storage and retention limits** that have changed over time — **verify Render's current terms** before relying on it for anything to keep.
- Suitable to **stand up, demo, and test**; real traffic needs a paid tier (no idle sleep, persistent resources, larger DB).
- Paid and free tiers run the **same code** — upgrading changes the plan/config, not the backend.

---

## 10. Open Items to Confirm

Default behavior is as written; change any in one line.

1. **D8 — tax:** charge/display tax, or drop it? If charged, what rate?
2. **D4 — reviews:** open to all customers, or verified purchasers only?
3. **D5 — currency:** confirm single currency and which one (USD assumed).
4. **D6 — Condition/Features:** Condition as an enum is in; Features deferred — acceptable?
5. **D11 — Google sign-in & saved payment options:** keep as optional stubs, or build fully?
6. **D13 — variant matrix:** confirm the admin may stock a **subset** of combinations (assumed), rather than every combination being required.

*Resolved in v1.2: variants = full per-combination (b); images = Cloudflare R2.*
