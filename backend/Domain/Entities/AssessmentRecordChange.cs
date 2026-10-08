namespace FineManagement.Api.Domain.Entities;

/// <summary>考核记录的不可编辑操作历史，保留操作者姓名快照和字段差异。</summary>
public class AssessmentRecordChange
{
    public long Id { get; set; }
    public long AssessmentRecordId { get; set; }
    public AssessmentRecord Record { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public long? ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string ChangesJson { get; set; } = "[]";
}
