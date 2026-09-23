using System.ComponentModel.DataAnnotations;
using FineManagement.Api.Domain.Enums;

namespace FineManagement.Api.DTOs.Assessments;

public sealed class AssessmentRecordQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateOnly? DetailDateFrom { get; set; }
    public DateOnly? DetailDateTo { get; set; }
    public long? DepartmentId { get; set; }
    public List<long> DepartmentIds { get; set; } = [];
    public string? Name { get; set; }
    public AssessmentStatus? Status { get; set; }
    [RegularExpression("^(assessmentDate|detailDate|departmentName|personName|amount)$")]
    public string SortBy { get; set; } = "assessmentDate";
    [RegularExpression("^(ascending|descending)$")]
    public string SortOrder { get; set; } = "descending";
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 200)] public int PageSize { get; set; } = 10;
}

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
}

public sealed class UpdateAssessmentRecordRequest : CreateAssessmentRecordRequest
{
    public long? PaymentMethodId { get; set; }
}

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

public sealed record ExcelImportResult(
    long BatchId,
    int Staged,
    int Failed,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> PendingDepartments,
    IReadOnlyList<ExcelStagedRow> StagedRows,
    IReadOnlyList<ExcelImportFailedRow> FailedRows);

public sealed record ExcelImportConfirmResult(
    int Imported,
    IReadOnlyList<string> CreatedDepartments);
