using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FastCart.Infrastructure.Identity;

/// <summary>
/// Issues signed JWT access tokens and opaque refresh tokens (§4.4). Reads the Jwt
/// section (Secret/Issuer/Audience/AccessTokenMinutes/RefreshTokenDays, §9.4).
/// </summary>
public sealed class JwtTokenGenerator
{
    private readonly IConfiguration _config;

    public JwtTokenGenerator(IConfiguration config) => _config = config;

    public int RefreshTokenDays =>
        int.TryParse(_config["Jwt:RefreshTokenDays"], out var d) ? d : 14;

    public (string Token, DateTime ExpiresAt) CreateAccessToken(
        string userId, string userName, string? email, IEnumerable<string> roles)
    {
        var jwt = _config.GetSection("Jwt");
        var secret = jwt["Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            secret = new string('0', 32); // dev fallback, mirrors Program.cs
        }

        var minutes = int.TryParse(jwt["AccessTokenMinutes"], out var m) ? m : 60;
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, email ?? string.Empty),
            new("userName", userName)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string CreateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
