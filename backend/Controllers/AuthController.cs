using FineManagement.Api.Data;
using FineManagement.Api.Domain.Entities;
using FineManagement.Api.DTOs.Auth;
using FineManagement.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FineManagement.Api.Controllers;

/// <summary>用户登录与 JWT 签发接口。</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, ITokenService tokenService) : ControllerBase
{
    /// <summary>校验用户名和密码，返回当前用户信息及访问令牌。</summary>
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == request.Username, cancellationToken);
        if (user is null || !user.IsEnabled) return Unauthorized(new { message = "用户名或密码错误" });

        var verification = new PasswordHasher<AppUser>().VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed) return Unauthorized(new { message = "用户名或密码错误" });

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var token = tokenService.CreateToken(user, out var expiresAt);
        return Ok(new LoginResponse(token, expiresAt, user.Id, user.Username, user.Name, user.Role));
    }
}
