namespace FineManagement.Api.Domain.Entities;

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
