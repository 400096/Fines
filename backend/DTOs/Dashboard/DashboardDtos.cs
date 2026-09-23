using FineManagement.Api.Domain.Enums;

namespace FineManagement.Api.DTOs.Dashboard;

public sealed record DashboardSummaryResponse(
    int TodayCount,
    decimal MonthAmount,
    int MonthCount,
    int PendingCount,
    int PersonCount,
    int DepartmentCount,
    decimal MonthGrowthPercent);

public sealed record DashboardTrendPoint(DateOnly Date, decimal Amount);

public sealed record DashboardDistributionItem(string Name, decimal Amount, int Count);

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

public sealed record DashboardResponse(
    DashboardSummaryResponse Summary,
    IReadOnlyList<DashboardTrendPoint> Trend,
    IReadOnlyList<DashboardDistributionItem> Distribution,
    IReadOnlyList<DashboardRecentRecord> RecentRecords);
