namespace FastCart.Domain.Common;

/// <summary>
/// Role names seeded at startup (§4.4). A customer holds <see cref="Customer"/>;
/// an operator holds <see cref="Admin"/>.
/// </summary>
public static class Roles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";

    public static readonly string[] All = { Customer, Admin };
}
