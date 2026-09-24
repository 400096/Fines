using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FineManagement.Api.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace FineManagement.Api.Services;

/// <summary>根据用户信息签发带角色声明的 JWT 访问令牌。</summary>
public sealed class TokenService(IConfiguration configuration) : ITokenService
{
    /// <summary>创建访问令牌并返回其 UTC 过期时间。</summary>
    public string CreateToken(AppUser user, out DateTime expiresAt)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Missing Jwt:Key configuration.");
        var expiresMinutes = configuration.GetValue("Jwt:ExpiresMinutes", 480);
        expiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.Name),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
