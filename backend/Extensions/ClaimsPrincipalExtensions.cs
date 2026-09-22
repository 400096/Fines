using System.Security.Claims;

namespace FineManagement.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static long? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id) ? id : null;
    }
}
