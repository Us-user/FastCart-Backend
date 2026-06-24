using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FastCart.Api.Common;
using FastCart.Api.Middleware;
using FastCart.Application.Common;
using FastCart.Infrastructure;
using FastCart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------

// Controllers + envelope-shaped validation errors (§4.3).
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Pin all inbound/outbound JSON dates to UTC for Npgsql timestamptz (§8).
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        o.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(kvp => kvp.Value is not null && kvp.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            return new BadRequestObjectResult(ApiResponse.Fail("Validation failed.", errors));
        };
    });

// Infrastructure composition root (DbContext/Identity/storage wire in later phases).
builder.Services.AddInfrastructure(builder.Configuration);

// CORS from Cors:AllowedOrigins (§8, §9.4).
const string CorsPolicy = "FastCartCors";
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

// JWT bearer auth (§4.4). Token issuance arrives in Phase 2; the scheme and the
// Swagger "Authorize" button are wired now so protected endpoints work later.
var jwt = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwt["Secret"];

// Fail fast outside Development if the signing key is missing/weak (no silent fallback).
if (!builder.Environment.IsDevelopment() && (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32))
{
    throw new InvalidOperationException(
        "Jwt:Secret must be configured with at least 32 characters outside Development.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jwtSecret)
                    ? new string('0', 32)
                    : jwtSecret))
        };

        // Envelope-shaped auth failures so 401/403 match the §4.3 response contract.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(ApiResponse.Fail("Authentication is required."));
                }
            },
            OnForbidden = async context =>
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(ApiResponse.Fail("You do not have access to this resource."));
                }
            }
        };
    });
// Secure-by-default: any endpoint without explicit authorization metadata still requires
// an authenticated user. Public endpoints opt out with [AllowAnonymous] (§8).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Rate limiting on the auth surface (§8) — fixed window per client IP, enveloped 429.
var authPermit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
var authWindow = builder.Configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermit,
                Window = TimeSpan.FromSeconds(authWindow),
                QueueLimit = 0
            }));
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse.Fail("Too many requests. Please slow down and try again shortly."), ct);
    };
});

// Structured request logging for observability (§8): method, path, status, duration.
builder.Services.AddHttpLogging(o =>
    o.LoggingFields = HttpLoggingFields.RequestMethod
                    | HttpLoggingFields.RequestPath
                    | HttpLoggingFields.ResponseStatusCode
                    | HttpLoggingFields.Duration);

// Machine-readable logs in Production for Render's log aggregation (§8).
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}

// Swagger/OpenAPI at /swagger with a JWT bearer auth button (§4.2).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FastCart API",
        Version = "v1",
        Description = "FastCart e-commerce backend API."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT access token (without the 'Bearer ' prefix)."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = new List<string>()
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// HTTP pipeline
// ---------------------------------------------------------------------------

// Envelope-shaped error handling wraps the whole pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpLogging();

// Swagger is available the moment the app runs, in every environment (§4.2).
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "FastCart API v1");
    options.DocumentTitle = "FastCart API";
});

// Serve locally-stored uploads (dev image storage, §9.3/D12) from wwwroot.
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Apply migrations and seed roles/admin on startup when a database is configured (§9.2).
// Wrapped so the app (and Swagger) still start even if the database is unreachable.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var startupLogger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var connectionString = app.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        startupLogger.LogWarning(
            "No ConnectionStrings:DefaultConnection configured; skipping migrate/seed. Swagger is still available.");
    }
    else
    {
        try
        {
            var db = services.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            await DbSeeder.SeedAsync(services);
            startupLogger.LogInformation("Database migrated and seeded.");
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex,
                "Database migrate/seed failed at startup; continuing so the API can still serve Swagger.");
        }
    }
}

app.Run();
