using System.Collections.Generic;
using System.Linq;

namespace FastCart.Domain.Common;

/// <summary>
/// Role names seeded at startup (§4.4) and their privilege hierarchy.
/// Customer &lt; Admin &lt; SuperAdmin &lt; Boss. All of Admin/SuperAdmin/Boss are
/// "management" roles that pass the admin-gated endpoints; the differences between
/// them are about who may grant/revoke which roles (see AdminUserService).
/// </summary>
public static class Roles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string Boss = "Boss";

    public static readonly string[] All = { Customer, Admin, SuperAdmin, Boss };

    /// <summary>
    /// Comma-separated roles that grant operational/admin access. Use in
    /// <c>[Authorize(Roles = Roles.Management)]</c> (ASP.NET treats commas as OR).
    /// </summary>
    public const string Management = Admin + "," + SuperAdmin + "," + Boss;

    /// <summary>Privilege level (higher = more powerful). Unknown roles are 0.</summary>
    public static int Level(string? role) => role switch
    {
        Boss => 3,
        SuperAdmin => 2,
        Admin => 1,
        Customer => 0,
        _ => 0
    };

    /// <summary>The highest privilege level held across a set of roles.</summary>
    public static int HighestLevel(IEnumerable<string> roles) =>
        roles.Select(Level).DefaultIfEmpty(0).Max();
}
