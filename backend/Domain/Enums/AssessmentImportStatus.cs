namespace FineManagement.Api.Domain.Enums;

/// <summary>Excel 导入批次状态。</summary>
public enum AssessmentImportStatus
{
    /// <summary>等待用户校对确认。</summary>
    Pending,
    /// <summary>已经写入正式记录表。</summary>
    Confirmed,
    /// <summary>用户已经取消导入。</summary>
    Cancelled
}
