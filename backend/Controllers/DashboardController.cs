using FineManagement.Api.Data;
using FineManagement.Api.Domain.Enums;
using FineManagement.Api.DTOs.Dashboard;
using FineManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FineManagement.Api.Controllers;

/// <summary>首页汇总指标、趋势图、事由占比和最近记录接口。</summary>
[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    /// <summary>获取首页所需的全部统计数据。</summary>
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);
        var trendStart = today.AddDays(-6);

        var todayCount = await db.AssessmentRecords.CountAsync(
            x => x.DetailDate == today, cancellationToken);
        var monthCount = await db.AssessmentRecords.CountAsync(
            x => x.DetailDate >= monthStart && x.DetailDate <= today, cancellationToken);
        var monthAmount = await db.AssessmentRecords
            .Where(x => x.DetailDate >= monthStart && x.DetailDate <= today)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var previousMonthAmount = await db.AssessmentRecords
            .Where(x => x.DetailDate >= previousMonthStart && x.DetailDate < monthStart)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var pendingCount = await db.AssessmentRecords.CountAsync(
            x => x.Status == AssessmentStatus.Pending, cancellationToken);
        var monthPersonNames = await db.AssessmentRecords
            .Where(x => x.DetailDate >= monthStart && x.DetailDate <= today)
            .Select(x => x.PersonName)
            .ToListAsync(cancellationToken);
        var personCount = monthPersonNames
            .SelectMany(AssessmentAmountFormatter.SplitPersonNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var departmentCount = await db.AssessmentRecords
            .Where(x => x.DetailDate >= monthStart && x.DetailDate <= today)
            .Select(x => x.DepartmentId)
            .Distinct()
            .CountAsync(cancellationToken);

        var growthPercent = previousMonthAmount == 0
            ? monthAmount == 0 ? 0 : 100
            : Math.Round((monthAmount - previousMonthAmount) / previousMonthAmount * 100, 1);

        var trendRows = await db.AssessmentRecords.AsNoTracking()
            .Where(x => x.DetailDate >= trendStart && x.DetailDate <= today)
            .GroupBy(x => x.DetailDate)
            .Select(x => new DashboardTrendPoint(x.Key, x.Sum(record => record.Amount)))
            .ToListAsync(cancellationToken);
        var trendAmounts = trendRows.ToDictionary(x => x.Date, x => x.Amount);
        var trend = Enumerable.Range(0, 7)
            .Select(offset => trendStart.AddDays(offset))
            .Select(date => new DashboardTrendPoint(
                date,
                trendAmounts.GetValueOrDefault(date)))
            .ToList();

        var distributionRows = await db.AssessmentRecords.AsNoTracking()
            .Where(x => x.DetailDate >= monthStart && x.DetailDate <= today)
            .GroupBy(x => x.Reason)
            .Select(x => new
            {
                Name = x.Key,
                Amount = x.Sum(record => record.Amount),
                Count = x.Count()
            })
            .ToListAsync(cancellationToken);
        var distribution = distributionRows
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .Select(x => new DashboardDistributionItem(x.Name, x.Amount, x.Count))
            .ToList();

        var recentRows = await db.AssessmentRecords.AsNoTracking()
            .OrderByDescending(x => x.DetailDate)
            .ThenByDescending(x => x.Id)
            .Take(5)
            .Select(x => new
            {
                x.Id,
                x.AssessmentDateType,
                x.AssessmentDate,
                x.AssessmentEndDate,
                x.DetailDate,
                DepartmentName = x.Department.Name,
                x.PersonName,
                x.Amount,
                x.Reason,
                x.Status,
                PaymentMethodName = x.PaymentMethod != null ? x.PaymentMethod.Name : null
            })
            .ToListAsync(cancellationToken);
        var recentRecords = recentRows
            .Select(x => new DashboardRecentRecord(
                x.Id,
                x.AssessmentDateType,
                x.AssessmentDate,
                x.AssessmentEndDate,
                x.DetailDate,
                x.DepartmentName,
                x.PersonName,
                x.Amount,
                AssessmentAmountFormatter.Format(x.Amount, x.PersonName),
                x.Reason,
                x.Status,
                x.PaymentMethodName))
            .ToList();

        var summary = new DashboardSummaryResponse(
            todayCount,
            monthAmount,
            monthCount,
            pendingCount,
            personCount,
            departmentCount,
            growthPercent);

        return Ok(new DashboardResponse(summary, trend, distribution, recentRecords));
    }
}
