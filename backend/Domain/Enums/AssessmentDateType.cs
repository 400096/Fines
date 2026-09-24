namespace FineManagement.Api.Domain.Enums;

/// <summary>考核发生日期的表达方式。</summary>
public enum AssessmentDateType
{
    /// <summary>单个固定日期。</summary>
    Fixed,
    /// <summary>包含开始和结束日期的连续区间。</summary>
    Range
}
