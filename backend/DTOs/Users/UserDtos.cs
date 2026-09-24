using System.ComponentModel.DataAnnotations;

namespace FineManagement.Api.DTOs.Users;

/// <summary>用户分页查询条件。</summary>
public sealed class UserQuery
{
    public string? Keyword { get; set; }
    public string? Role { get; set; }
    public bool? IsEnabled { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 200)] public int PageSize { get; set; } = 10;
}

/// <summary>新增用户请求。</summary>
public sealed class CreateUserRequest
{
    [Required, MaxLength(50)] public string Username { get; set; } = string.Empty;
    [Required, MinLength(8), MaxLength(100)] public string Password { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    [MaxLength(30)] public string? Phone { get; set; }
    [Required, MaxLength(50)] public string Role { get; set; } = "查看人员";
    public bool IsEnabled { get; set; } = true;
}

/// <summary>编辑用户请求；密码为空时不修改密码。</summary>
public sealed class UpdateUserRequest
{
    [MaxLength(100), MinLength(8)] public string? Password { get; set; }
    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    [MaxLength(30)] public string? Phone { get; set; }
    [Required, MaxLength(50)] public string Role { get; set; } = "查看人员";
    public bool IsEnabled { get; set; } = true;
}

/// <summary>用户返回模型，不包含密码哈希。</summary>
public sealed record UserResponse(long Id, string Username, string Name, string? Phone, string Role, bool IsEnabled, DateTime? LastLoginAt, DateTime CreatedAt);
