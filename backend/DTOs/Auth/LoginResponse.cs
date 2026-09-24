namespace FineManagement.Api.DTOs.Auth;

/// <summary>登录成功后的令牌和当前用户信息。</summary>
public sealed record LoginResponse(
    string Token,
    DateTime ExpiresAt,
    long UserId,
    string Username,
    string Name,
    string Role);
