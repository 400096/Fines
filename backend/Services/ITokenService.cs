using FineManagement.Api.Domain.Entities;

namespace FineManagement.Api.Services;

public interface ITokenService
{
    string CreateToken(AppUser user, out DateTime expiresAt);
}
