using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SIGM.Api.Services;

public sealed class TokenService(IConfiguration cfg)
{
    public (string token, DateTimeOffset expiresAt) Create(Guid userId, string email, string fullName, Guid? tenantId, string? productType, IEnumerable<string> permissions, int tokenVersion = 0)
    {
        var jwtKey = cfg["Jwt:Key"];
        const string DevOnly = "DEV_ONLY_CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32+";
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            jwtKey = DevOnly; // solo aceptable en Development (validado al arranque)
        }
        else if (jwtKey == DevOnly)
        {
            throw new InvalidOperationException("Jwt:Key tiene el valor por defecto DEV_ONLY_CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32+. Reemplázalo en la configuración.");
        }
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)) { KeyId = "sigm-cloud-signing-key" };
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTimeOffset.UtcNow.AddHours(8);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("name", fullName),
            new("tv", tokenVersion.ToString()),
        };
        if (tenantId is Guid tid) claims.Add(new Claim("tenant_id", tid.ToString()));
        if (!string.IsNullOrWhiteSpace(productType)) claims.Add(new Claim("product_type", productType));
        foreach (var p in permissions.Distinct()) claims.Add(new Claim("permission", p));

        var jwt = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"] ?? "sigm-cloud",
            audience: cfg["Jwt:Audience"] ?? "sigm-cloud-clients",
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
