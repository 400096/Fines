using System.ComponentModel.DataAnnotations;
using FineManagement.Api.Domain.Enums;

namespace FineManagement.Api.DTOs.Assessments;

/// <summary>考核记录列表的筛选、排序和分页参数。</summary>
public sealed class AssessmentRecordQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateOnly? DetailDateFrom { get; set; }
    public DateOnly? DetailDateTo { get; set; }
    public long? DepartmentId { get; set; }
    public List<long> DepartmentIds { get; set; } = [];
    public string? Name { get; set; }
    [MaxLength(500)] public string? Remark { get; set; }
    public AssessmentStatus? Status { get; set; }
    [RegularExpression("^(assessmentDate|detailDate|departmentName|personName|amount)$")]
    public string SortBy { get; set; } = "assessmentDate";
    [RegularExpression("^(ascending|descending)$")]
    public string SortOrder { get; set; } = "descending";
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 200)] public int PageSize { get; set; } = 10;
}

/// <summary>新增考核记录请求。</summary>
public class CreateAssessmentRecordRequest
{
    [Required] public AssessmentDateType AssessmentDateType { get; set; } = AssessmentDateType.Fixed;
    [Required] public DateOnly AssessmentDate { get; set; }
    public DateOnly? AssessmentEndDate { get; set; }
    [Required] public DateOnly DetailDate { get; set; }
    [Range(1, long.MaxValue)] public long DepartmentId { get; set; }
    [Required, MaxLength(50)] public string PersonName { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "999999999")] public decimal Amount { get; set; }
    [Required, MaxLength(300)] public string Reason { get; set; } = string.Empty;
    [MaxLength(500)] public string? Remark { get; set; }
    public long? PaymentMethodId { get; set; }
}

/// <summary>编辑考核记录请求，收款方式决定记录是否已完成。</summary>
public sealed class UpdateAssessmentRecordRequest : CreateAssessmentRecordRequest
{
}

/// <summary>手工添加或编辑 Excel 待确认数据的请求。</summary>
public sealed class SaveAssessmentImportRowRequest
{
    [Required] public AssessmentDateType AssessmentDateType { get; set; } = AssessmentDateType.Fixed;
    [Required] public DateOnly AssessmentDate { get; set; }
    public DateOnly? AssessmentEndDate { get; set; }
    [Required] public DateOnly DetailDate { get; set; }
    public long? DepartmentId { get; set; }
    [Required, MaxLength(100)] public string DepartmentName { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string PersonName { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "999999999")] public decimal Amount { get; set; }
    [Required, MaxLength(300)] public string Reason { get; set; } = string.Empty;
    [MaxLength(500)] public string? Remark { get; set; }
    public long? PaymentMethodId { get; set; }
}

/// <summary>考核记录返回模型。</summary>
public sealed record AssessmentRecordResponse(
    long Id,
    AssessmentDateType AssessmentDateType,
    DateOnly AssessmentDate,
    DateOnly? AssessmentEndDate,
    DateOnly DetailDate,
    long DepartmentId,
    string DepartmentName,
    string PersonName,
    decimal Amount,
    string AmountDisplay,
    string Reason,
    string? Remark,
    AssessmentStatus Status,
    long? PaymentMethodId,
    string? PaymentMethodName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Excel 导入批次中的一条待确认数据。</summary>
public sealed record ExcelStagedRow(
    long StagingId,
    int RowNumber,
    AssessmentDateType AssessmentDateType,
    DateOnly AssessmentDate,
    DateOnly? AssessmentEndDate,
    DateOnly DetailDate,
    long DepartmentId,
    string DepartmentName,
    string PersonName,
    decimal Amount,
    string AmountDisplay,
    string Reason,
    string? Remark,
    long? PaymentMethodId,
    string? PaymentMethodName);

/// <summary>无法自动识别、需要人工处理的 Excel 行。</summary>
public sealed record ExcelImportFailedRow(
    int RowNumber,
    string Message,
    string? AssessmentDate,
    string? DetailDate,
    string? DepartmentName,
    string? PersonName,
    string? AmountText,
    string? Reason,
    string? Remark,
    string? PaymentMethodName);

/// <summary>Excel 解析及暂存结果。</summary>
public sealed record ExcelImportResult(
    long BatchId,
    int Staged,
    int Failed,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> PendingDepartments,
    IReadOnlyList<ExcelStagedRow> StagedRows,
    IReadOnlyList<ExcelImportFailedRow> FailedRows);

/// <summary>确认导入后的正式入库结果。</summary>
public sealed record ExcelImportConfirmResult(
    int Imported,
    IReadOnlyList<string> CreatedDepartments);
