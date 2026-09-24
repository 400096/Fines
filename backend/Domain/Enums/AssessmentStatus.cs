namespace FineManagement.Api.Domain.Enums;

/// <summary>考核记录的财务处理状态。</summary>
public enum AssessmentStatus
{
    /// <summary>尚未选择收款方式。</summary>
    Pending = 0,
    /// <summary>已选择收款方式并完成处理。</summary>
    Completed = 1
}
