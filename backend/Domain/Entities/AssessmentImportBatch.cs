using FineManagement.Api.Domain.Enums;

namespace FineManagement.Api.Domain.Entities;

/// <summary>一次 Excel 导入产生的待确认批次。</summary>
public class AssessmentImportBatch : ISoftDeletable
{
    public long Id { get; set; }
    public AssessmentImportStatus Status { get; set; } = AssessmentImportStatus.Pending;
    public long? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public ICollection<AssessmentImportRow> Rows { get; set; } = [];
}
