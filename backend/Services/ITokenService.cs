using FineManagement.Api.Domain.Entities;

namespace FineManagement.Api.Services;

/// <summary>JWT 令牌生成服务。</summary>
public interface ITokenService
{
    string CreateToken(AppUser user, out DateTime expiresAt);
}
