using System.ComponentModel.DataAnnotations;

namespace FineManagement.Api.DTOs.Departments;

/// <summary>部门分页查询条件。</summary>
public sealed class DepartmentQuery
{
    public string? Keyword { get; set; }
    public bool? IsEnabled { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 200)] public int PageSize { get; set; } = 10;
}

/// <summary>新增或编辑部门请求。</summary>
public class SaveDepartmentRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Code { get; set; } = string.Empty;
    [Range(0, 9999)] public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    [MaxLength(50)] public string? Manager { get; set; }
    [MaxLength(300)] public string? Remark { get; set; }
}

/// <summary>部门返回模型。</summary>
public sealed record DepartmentResponse(long Id, string Name, string Code, int SortOrder, bool IsEnabled, string? Manager, string? Remark, DateTime UpdatedAt);
