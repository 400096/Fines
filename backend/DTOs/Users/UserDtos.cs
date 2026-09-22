using System.ComponentModel.DataAnnotations;

namespace FineManagement.Api.DTOs.Users;

public sealed class UserQuery
{
    public string? Keyword { get; set; }
    public string? Role { get; set; }
    public bool? IsEnabled { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 200)] public int PageSize { get; set; } = 10;
}

public sealed class CreateUserRequest
{
    [Required, MaxLength(50)] public string Username { get; set; } = string.Empty;
    [Required, MinLength(8), MaxLength(100)] public string Password { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    [MaxLength(30)] public string? Phone { get; set; }
    [Required, MaxLength(50)] public string Role { get; set; } = "查看人员";
    public bool IsEnabled { get; set; } = true;
}

public sealed class UpdateUserRequest
{
    [MaxLength(100), MinLength(8)] public string? Password { get; set; }
    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    [MaxLength(30)] public string? Phone { get; set; }
    [Required, MaxLength(50)] public string Role { get; set; } = "查看人员";
    public bool IsEnabled { get; set; } = true;
}

public sealed record UserResponse(long Id, string Username, string Name, string? Phone, string Role, bool IsEnabled, DateTime? LastLoginAt, DateTime CreatedAt);
