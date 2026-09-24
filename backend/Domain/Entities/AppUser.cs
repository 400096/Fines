namespace FineManagement.Api.Domain.Entities;

/// <summary>可登录系统的用户账号。</summary>
public class AppUser : ISoftDeletable
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = "查看人员";
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public ICollection<AssessmentRecord> CreatedAssessmentRecords { get; set; } = [];
}
