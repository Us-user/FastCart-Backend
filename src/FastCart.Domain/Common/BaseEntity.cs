namespace FastCart.Domain.Common;

/// <summary>
/// Base for int-keyed catalog/commerce entities (§4.3: "IDs: int for catalog/commerce
/// entities, string GUID for users"). Carries the identity key and audit timestamps.
/// </summary>
public abstract class BaseEntity : IAuditable
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
