using FineManagement.Api.Domain.Enums;

namespace FineManagement.Api.DTOs.Dashboard;

/// <summary>首页顶部汇总指标。</summary>
public sealed record DashboardSummaryResponse(
    int TodayCount,
    decimal MonthAmount,
    int MonthCount,
    int PendingCount,
    int PersonCount,
    int DepartmentCount,
    decimal MonthGrowthPercent);

/// <summary>最近七天金额趋势点。</summary>
public sealed record DashboardTrendPoint(DateOnly Date, decimal Amount);

/// <summary>按考核事由汇总的分布项。</summary>
public sealed record DashboardDistributionItem(string Name, decimal Amount, int Count);

/// <summary>首页最近考核记录。</summary>
public sealed record DashboardRecentRecord(
    long Id,
    AssessmentDateType AssessmentDateType,
    DateOnly AssessmentDate,
    DateOnly? AssessmentEndDate,
    DateOnly DetailDate,
    string DepartmentName,
    string PersonName,
    decimal Amount,
    string AmountDisplay,
    string Reason,
    AssessmentStatus Status,
    string? PaymentMethodName);

/// <summary>首页完整响应。</summary>
public sealed record DashboardResponse(
    DashboardSummaryResponse Summary,
    IReadOnlyList<DashboardTrendPoint> Trend,
    IReadOnlyList<DashboardDistributionItem> Distribution,
    IReadOnlyList<DashboardRecentRecord> RecentRecords);
