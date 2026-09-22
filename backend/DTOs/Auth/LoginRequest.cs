using System.ComponentModel.DataAnnotations;

namespace FineManagement.Api.DTOs.Auth;

public sealed class LoginRequest
{
    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}
