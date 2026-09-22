namespace FineManagement.Api.DTOs.Auth;

public sealed record LoginResponse(
    string Token,
    DateTime ExpiresAt,
    long UserId,
    string Username,
    string Name,
    string Role);
