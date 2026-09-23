using FineManagement.Api.Domain.Enums;

namespace FineManagement.Api.Domain.Entities;

public class AssessmentRecord : ISoftDeletable
{
    public long Id { get; set; }
    public AssessmentDateType AssessmentDateType { get; set; } = AssessmentDateType.Fixed;
    public DateOnly AssessmentDate { get; set; }
    public DateOnly? AssessmentEndDate { get; set; }
    public DateOnly DetailDate { get; set; }
    public long DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string PersonName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public AssessmentStatus Status { get; set; } = AssessmentStatus.Pending;
    public long? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public long? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
