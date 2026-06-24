using FastCart.Domain.Common;

namespace FastCart.Domain.Entities;

/// <summary>Home main-carousel slide (§5.6).</summary>
public class Slider : BaseEntity
{
    public string ImageUrl { get; set; } = default!;
    public string? Subtitle { get; set; }
    public string? Title { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Promo banner with countdown — backs Flash Sales (§5.6, §6.12).</summary>
public class Banner : BaseEntity
{
    public string ImageUrl { get; set; } = default!;
    public string? Title { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Countdown target (§5.6).</summary>
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Newsletter signup, unique by email (§5.6, §6.13).</summary>
public class NewsletterSubscriber : BaseEntity
{
    public string Email { get; set; } = default!;
}

/// <summary>Contact-form message (§5.6, §6.13).</summary>
public class ContactMessage : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string Message { get; set; } = default!;
    public bool IsRead { get; set; }
}
