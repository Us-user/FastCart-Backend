---
name: run-fastcart-api
description: Build, launch, and smoke-test the FastCart backend API (.NET 10 / ASP.NET Core). Use when asked to run, start, build, boot, test, or smoke-test the FastCart API, hit /health or /swagger, or verify the server comes up.
---

# Run the FastCart API

FastCart is a layered ASP.NET Core (.NET 10) Web API. The deployable unit is
`FastCart.Api`; `FastCart.Application/Domain/Infrastructure` are libraries it
references. It's a **server**, so the driver is a curl-based smoke script —
`.claude/skills/run-fastcart-api/smoke.sh` — which builds, launches the API,
polls `/health`, asserts the real response bodies (envelope + Swagger + JWT
security scheme), then tears the server down.

**All paths below are relative to the unit root** (the directory containing
`FastCart.slnx`). The driver itself `cd`s to that root, so it runs from anywhere.

## Prerequisites

- **.NET SDK 10** (verified with `10.0.204`). Check:
  ```bash
  dotnet --version   # must print 10.x
  ```
- `bash` + `curl` to run the driver (Git Bash on Windows; native on Linux/macOS).
- **No database needed to boot.** Phase 0 has no `DbContext` — the app starts
  without PostgreSQL. (From Phase 1 onward it will need Postgres; the dev
  connection string lives in `src/FastCart.Api/appsettings.Development.json`.)

## Run (agent path) — the driver

```bash
bash .claude/skills/run-fastcart-api/smoke.sh
```

It builds, launches `FastCart.Api.dll` on `http://localhost:5046`, waits for
readiness, then runs 9 checks. Expected tail:

```
==> 9 passed, 0 failed
ALL CHECKS PASSED
```

What it asserts (the real running surface, not the test suite):
- `GET /health` → `200`, body `{"success":true,...,"data":{"status":"Healthy",...}}`
- `GET /swagger/index.html` → `200` (Swagger UI loads)
- `GET /swagger/v1/swagger.json` → `200`, contains a `Bearer` security scheme
  with `"bearerFormat":"JWT"` and documents `/health`.

On any failure it prints `FAIL:` lines and dumps the server log, then exits 1.
The `trap` always kills the server and frees the port (verified). Change the
port with `PORT=8080 bash .claude/skills/run-fastcart-api/smoke.sh`.

To drive a single route by hand while it's up, the driver launches with:
```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5046 \
  dotnet src/FastCart.Api/bin/Debug/net10.0/FastCart.Api.dll
# then in another shell:
curl -s http://localhost:5046/health
```

## Run (human path)

```bash
dotnet run --project src/FastCart.Api --launch-profile http --no-build
```
Serves `http://localhost:5046` — open `/swagger` in a browser, Ctrl-C to stop.
Drop `--no-build` to compile first. (Useless headless — use the driver instead.)

## Gotchas (battle scars from building this)

- **`.NET 10` makes `.slnx`, not `.sln`.** `dotnet new sln` emits `FastCart.slnx`
  (new XML format). `dotnet sln FastCart.sln add …` fails with "Could not find
  solution"; use `dotnet sln FastCart.slnx add …`.
- **Running the DLL directly ignores `launchSettings.json`.** No launch profile
  is applied, so Kestrel would default to `:5000`. The driver sets
  `ASPNETCORE_URLS` to pin the port — required, not optional.
- **HTTPS-redirect warning is benign.** Under an http-only bind you'll see
  `warn: …HttpsRedirectionMiddleware … Failed to determine the https port for
  redirect.` Expected; requests still return 200.
- **Swashbuckle 10 → Microsoft.OpenApi 2.x is a breaking API change** (see
  Troubleshooting). If you touch the Swagger/JWT wiring in `Program.cs`, expect
  the v1 (`Microsoft.OpenApi.Models`) patterns from old tutorials to NOT compile.
- **Don't reflect the OpenAPI assembly from Windows PowerShell.** PowerShell 5.1
  runs on .NET Framework and throws `ReflectionTypeLoadException` loading the
  `net10.0` `Microsoft.OpenApi.dll`. Read the package's `.xml` doc in the NuGet
  cache instead (`~/.nuget/packages/microsoft.openapi/<ver>/lib/net8.0/*.xml`).

## Troubleshooting (errors actually hit → fix)

- `CS0234: namespace "Models" does not exist in "Microsoft.OpenApi"`
  → change `using Microsoft.OpenApi.Models;` to `using Microsoft.OpenApi;`
  (2.x flattened the namespace).
- `CS0246: BadRequestObjectResult not found` in `Program.cs`
  → add `using Microsoft.AspNetCore.Mvc;`.
- `CS0029: cannot convert string[] to List<string>` (security requirement value)
  → use `new List<string>()`, not `Array.Empty<string>()`.
- `CS1503: cannot convert OpenApiSecurityRequirement to Func<OpenApiDocument,
  OpenApiSecurityRequirement>`
  → `AddSecurityRequirement` now takes a lambda; wrap it:
  `options.AddSecurityRequirement(document => new OpenApiSecurityRequirement {
  [new OpenApiSecuritySchemeReference("Bearer", document, null)] = new List<string>() });`
- Port already in use on relaunch → a prior server didn't die. The driver's
  `trap` normally prevents this; if a manual run left one behind, on Windows:
  `Get-NetTCPConnection -LocalPort 5046 -State Listen | %{ Stop-Process -Id $_.OwningProcess -Force }`
  (Linux/macOS: `kill $(lsof -ti :5046)`).
