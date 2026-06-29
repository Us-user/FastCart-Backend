using FastCart.Domain.Common;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Persistence;

/// <summary>
/// EF Core context for FastCart. Extends Identity (users/roles, string key) and adds
/// the full catalog/commerce/content model (§5). The schema is created entirely by
/// migrations (§9.2). Audit timestamps are stamped on save; decimal money uses (18,2).
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Accounts
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TelegramLinkToken> TelegramLinkTokens => Set<TelegramLinkToken>();
    public DbSet<PasswordResetCode> PasswordResetCodes => Set<PasswordResetCode>();
    public DbSet<Address> Addresses => Set<Address>();

    // Catalog
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<ProductOptionValue> ProductOptionValues => Set<ProductOptionValue>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductVariantOptionValue> ProductVariantOptionValues => Set<ProductVariantOptionValue>();

    // Commerce
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();

    // Ordering
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    // Content
    public DbSet<Slider> Sliders => Set<Slider>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money is decimal everywhere (§4.3); one convention covers all price/amount columns.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables first.

        ConfigureAccounts(builder);
        ConfigureCatalog(builder);
        ConfigureProducts(builder);
        ConfigureCommerce(builder);
        ConfigureOrdering(builder);
        ConfigureContent(builder);
    }

    private static void ConfigureAccounts(ModelBuilder b)
    {
        b.Entity<UserProfile>(e =>
        {
            e.Property(p => p.FirstName).HasMaxLength(100);
            e.Property(p => p.LastName).HasMaxLength(100);
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasOne<ApplicationUser>()
                .WithOne(u => u.Profile)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(t => t.Token).HasMaxLength(256);
            e.HasIndex(t => t.Token);
            e.HasOne<ApplicationUser>()
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Telegram chat_id is unique across accounts (multiple NULLs allowed by Postgres,
        // so unlinked users don't collide).
        b.Entity<ApplicationUser>()
            .HasIndex(u => u.TelegramChatId)
            .IsUnique();

        b.Entity<TelegramLinkToken>(e =>
        {
            e.Property(t => t.Token).HasMaxLength(128);
            e.HasIndex(t => t.Token).IsUnique();
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PasswordResetCode>(e =>
        {
            e.Property(c => c.CodeHash).HasMaxLength(128);
            e.Property(c => c.ChangeTokenHash).HasMaxLength(128);
            e.HasIndex(c => c.UserId);
            e.HasIndex(c => c.ChangeTokenHash);
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Address>(e =>
        {
            e.HasOne<ApplicationUser>()
                .WithMany(u => u.Addresses)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCatalog(ModelBuilder b)
    {
        b.Entity<Category>().Property(c => c.Name).HasMaxLength(100);

        b.Entity<SubCategory>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(100);
            e.HasOne(s => s.Category)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Brand>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<Color>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100);
            e.Property(c => c.HexCode).HasMaxLength(9);
        });
        b.Entity<Tag>().Property(t => t.Name).HasMaxLength(100);
    }

    private static void ConfigureProducts(ModelBuilder b)
    {
        b.Entity<Product>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100);
            e.Property(p => p.Code).HasMaxLength(64);
            e.Property(p => p.Condition).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(p => p.Name);
            e.HasIndex(p => p.CreatedAt);
            e.HasIndex(p => p.Price);
            e.Ignore(p => p.EffectivePrice);

            e.HasOne(p => p.SubCategory)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.SubCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.Brand)
                .WithMany()
                .HasForeignKey(p => p.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ProductImage>(e =>
            e.HasOne(i => i.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade));

        b.Entity<ProductTag>(e =>
        {
            e.HasKey(pt => new { pt.ProductId, pt.TagId });
            e.HasOne(pt => pt.Product)
                .WithMany(p => p.ProductTags)
                .HasForeignKey(pt => pt.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pt => pt.Tag)
                .WithMany()
                .HasForeignKey(pt => pt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProductOption>(e =>
        {
            e.Property(o => o.Name).HasMaxLength(100);
            e.HasOne(o => o.Product)
                .WithMany(p => p.Options)
                .HasForeignKey(o => o.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProductOptionValue>(e =>
        {
            e.Property(v => v.Value).HasMaxLength(100);
            e.HasOne(v => v.ProductOption)
                .WithMany(o => o.Values)
                .HasForeignKey(v => v.ProductOptionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.Color)
                .WithMany()
                .HasForeignKey(v => v.ColorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ProductVariant>(e =>
        {
            e.Property(v => v.Sku).HasMaxLength(64);
            e.HasIndex(v => v.Sku).IsUnique();
            e.HasOne(v => v.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProductVariantOptionValue>(e =>
        {
            e.HasKey(x => new { x.ProductVariantId, x.ProductOptionValueId });
            e.HasOne(x => x.ProductVariant)
                .WithMany(v => v.OptionValues)
                .HasForeignKey(x => x.ProductVariantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ProductOptionValue)
                .WithMany(v => v.VariantLinks)
                .HasForeignKey(x => x.ProductOptionValueId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCommerce(ModelBuilder b)
    {
        b.Entity<Cart>(e =>
        {
            e.HasIndex(c => c.UserId).IsUnique(); // one active cart per user (§5.3).
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CartItem>(e =>
        {
            e.HasOne(i => i.Cart)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.CartId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.ProductVariant)
                .WithMany()
                .HasForeignKey(i => i.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<WishlistItem>(e =>
        {
            e.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.Product)
                .WithMany()
                .HasForeignKey(w => w.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Review>(e =>
        {
            e.HasIndex(r => r.ProductId);
            e.HasOne(r => r.Product)
                .WithMany(p => p.Reviews)
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Coupon>(e =>
        {
            e.Property(c => c.Code).HasMaxLength(64);
            e.Property(c => c.DiscountType).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(c => c.Code).IsUnique();
        });

        b.Entity<CouponRedemption>(e =>
        {
            e.HasOne(r => r.Coupon)
                .WithMany(c => c.Redemptions)
                .HasForeignKey(r => r.CouponId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Order>()
                .WithMany()
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOrdering(ModelBuilder b)
    {
        b.Entity<Order>(e =>
        {
            e.Property(o => o.OrderNumber).HasMaxLength(32);
            e.Property(o => o.Currency).HasMaxLength(3);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.StatusBeforeReturn).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.CancelReason).HasMaxLength(300);
            e.Property(o => o.RejectReason).HasMaxLength(300);
            e.Property(o => o.ReturnReason).HasMaxLength(500);
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.HasIndex(o => o.Status);
            e.HasIndex(o => o.CreatedAt);

            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<OrderItem>(e =>
        {
            e.Property(i => i.Sku).HasMaxLength(64);
            e.Property(i => i.VariantDescription).HasMaxLength(256);
            e.HasOne(i => i.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Product>()
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne<ProductVariant>()
                .WithMany()
                .HasForeignKey(i => i.ProductVariantId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureContent(ModelBuilder b)
    {
        b.Entity<Banner>(e =>
            e.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull));

        b.Entity<NewsletterSubscriber>(e =>
        {
            e.Property(n => n.Email).HasMaxLength(256);
            e.HasIndex(n => n.Email).IsUnique();
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    private void ApplyAuditTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
