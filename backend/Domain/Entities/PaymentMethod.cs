namespace FineManagement.Api.Domain.Entities;

/// <summary>考核记录完成时选择的收款方式。</summary>
public class PaymentMethod : ISoftDeletable
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public ICollection<AssessmentRecord> AssessmentRecords { get; set; } = [];
}
