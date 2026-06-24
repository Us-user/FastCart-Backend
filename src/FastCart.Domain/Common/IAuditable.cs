namespace FastCart.Domain.Common;

/// <summary>
/// Audit timestamps (UTC) implied on all entities (§5). Stamped automatically by
/// the DbContext on save, so application code never sets them by hand.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
