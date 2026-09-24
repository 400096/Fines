using FineManagement.Api.Domain.Entities;
using FineManagement.Api.DTOs.Assessments;

namespace FineManagement.Api.Extensions;

/// <summary>服务器端排序扩展，保证排序覆盖完整筛选结果而不是只排序当前页。</summary>
public static class AssessmentRecordQueryExtensions
{
    public static IOrderedQueryable<AssessmentRecord> ApplySorting(
        this IQueryable<AssessmentRecord> records,
        AssessmentRecordQuery query)
    {
        var descending = !string.Equals(query.SortOrder, "ascending", StringComparison.OrdinalIgnoreCase);

        return query.SortBy switch
        {
            "detailDate" => descending
                ? records.OrderByDescending(x => x.DetailDate).ThenByDescending(x => x.Id)
                : records.OrderBy(x => x.DetailDate).ThenBy(x => x.Id),
            "departmentName" => descending
                ? records.OrderByDescending(x => x.Department.Name).ThenByDescending(x => x.Id)
                : records.OrderBy(x => x.Department.Name).ThenBy(x => x.Id),
            "personName" => descending
                ? records.OrderByDescending(x => x.PersonName).ThenByDescending(x => x.Id)
                : records.OrderBy(x => x.PersonName).ThenBy(x => x.Id),
            "amount" => descending
                ? records.OrderByDescending(x => x.Amount).ThenByDescending(x => x.Id)
                : records.OrderBy(x => x.Amount).ThenBy(x => x.Id),
            _ => descending
                ? records.OrderByDescending(x => x.AssessmentDate).ThenByDescending(x => x.Id)
                : records.OrderBy(x => x.AssessmentDate).ThenBy(x => x.Id),
        };
    }
}
