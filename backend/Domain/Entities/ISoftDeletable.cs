namespace FineManagement.Api.Domain.Entities;

/// <summary>支持逻辑删除的实体统一约定。</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
