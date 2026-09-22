namespace FineManagement.Api.Domain.Entities;

public class AssessmentImportRow : ISoftDeletable
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public AssessmentImportBatch Batch { get; set; } = null!;
    public int SourceRowNumber { get; set; }
    public DateOnly AssessmentDate { get; set; }
    public long? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string PersonName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public long? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
