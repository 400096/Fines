using System.Security.Claims;

namespace FineManagement.Api.Extensions;

/// <summary>从 JWT 声明中读取当前登录用户标识。</summary>
public static class ClaimsPrincipalExtensions
{
    public static long? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id) ? id : null;
    }
}
