using FastCart.Application.Addresses;
using FastCart.Application.AdminUsers;
using FastCart.Application.Auth;
using FastCart.Application.Carts;
using FastCart.Application.Catalog;
using FastCart.Application.Common.Interfaces;
using FastCart.Application.Communications;
using FastCart.Application.Content;
using FastCart.Application.Dashboard;
using FastCart.Application.Coupons;
using FastCart.Application.Orders;
using FastCart.Application.Payments;
using FastCart.Application.Profile;
using FastCart.Application.Reviews;
using FastCart.Application.Wishlists;
using FastCart.Infrastructure.Catalog;
using FastCart.Infrastructure.Commerce;
using FastCart.Infrastructure.Communications;
using FastCart.Infrastructure.Content;
using FastCart.Infrastructure.Dashboard;
using FastCart.Infrastructure.Identity;
using FastCart.Infrastructure.Messaging;
using FastCart.Infrastructure.Orders;
using FastCart.Infrastructure.Payments;
using FastCart.Infrastructure.Persistence;
using FastCart.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FastCart.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure-layer services. Phase 1 plugs in EF Core / Npgsql and
    /// ASP.NET Identity stores. Later phases add JWT/refresh services (Phase 2) and the
    /// R2 storage abstraction (Phase 3).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = BuildConnectionString(ResolveRawConnectionString(configuration));

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Phase 2 — auth, profile, addresses, email.
        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        // Storage — Cloudflare R2 when configured (§9.3/D12), else local disk for dev.
        var r2 = configuration.GetSection("Storage:R2");
        if (!string.IsNullOrWhiteSpace(r2["Endpoint"]) &&
            !string.IsNullOrWhiteSpace(r2["AccessKeyId"]) &&
            !string.IsNullOrWhiteSpace(r2["Bucket"]))
        {
            services.AddScoped<IStorageService, R2StorageService>();
        }
        else
        {
            services.AddScoped<IStorageService, LocalFileStorageService>();
        }

        // Phase 3 — catalog taxonomy.
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISubCategoryService, SubCategoryService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IColorService, ColorService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IProductService, ProductService>();

        // Phase 4 — cart, wishlist, reviews, coupons.
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IWishlistService, WishlistService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<ICouponService, CouponService>();

        // Phase 5 — payment providers (record-and-hold, §7.3/D2) + orders/checkout.
        services.AddScoped<IPaymentProvider, CashOnDeliveryPaymentProvider>();
        services.AddScoped<IPaymentProvider, ManualPaymentProvider>();
        services.AddScoped<IPaymentProviderResolver, PaymentProviderResolver>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAdminOrderService, AdminOrderService>();

        // Phase 6 — CMS/content, newsletter, contact, admin users (§6.12–6.14).
        services.AddScoped<ISliderService, SliderService>();
        services.AddScoped<IBannerService, BannerService>();
        services.AddScoped<INewsletterService, NewsletterService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IAdminUserService, AdminUserService>();

        // Phase 7 — admin dashboard & reporting (§6.15).
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }

    /// <summary>
    /// Resolves the raw connection string. Prefers <c>ConnectionStrings:DefaultConnection</c>
    /// (env <c>ConnectionStrings__DefaultConnection</c>); falls back to the conventional
    /// <c>DATABASE_URL</c> env var used by Render/Heroku/Railway. Null/empty if neither is set.
    /// </summary>
    public static string? ResolveRawConnectionString(IConfiguration configuration)
    {
        var fromConnectionStrings = configuration.GetConnectionString("DefaultConnection");
        return !string.IsNullOrWhiteSpace(fromConnectionStrings)
            ? fromConnectionStrings
            : configuration["DATABASE_URL"];
    }

    /// <summary>
    /// Normalizes the connection string for Npgsql. Managed hosts (Render, Heroku, Railway)
    /// hand out a <c>postgres(ql)://user:pass@host:port/db</c> URL, which Npgsql cannot parse —
    /// convert it to key-value form and enable TLS. Already-key-value strings pass through;
    /// an empty value falls back to the local dev database (§9.2/§9.4).
    /// </summary>
    private static string BuildConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Host=localhost;Port=5432;Database=fastcart;Username=postgres;Password=postgres";
        }

        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return raw; // Already an Npgsql key-value connection string.
        }

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            SslMode = Npgsql.SslMode.Require,
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }
}
