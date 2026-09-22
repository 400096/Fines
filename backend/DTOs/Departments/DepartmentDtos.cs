using System.ComponentModel.DataAnnotations;

namespace FineManagement.Api.DTOs.Departments;

public class SaveDepartmentRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Code { get; set; } = string.Empty;
    [Range(0, 9999)] public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    [MaxLength(50)] public string? Manager { get; set; }
    [MaxLength(300)] public string? Remark { get; set; }
}

public sealed record DepartmentResponse(long Id, string Name, string Code, int SortOrder, bool IsEnabled, string? Manager, string? Remark, DateTime UpdatedAt);
