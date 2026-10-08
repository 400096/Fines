using System.Globalization;
using System.Text.Json;
using FineManagement.Api.Domain.Entities;

namespace FineManagement.Api.Services;

/// <summary>把考核记录的业务字段转换成可读的变更明细。</summary>
public static class AssessmentChangeTracker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public sealed record FieldChange(string Field, string? Before, string? After);

    public static Dictionary<string, string?> Capture(AssessmentRecord record, string departmentName, string? paymentMethodName) => new()
    {
        ["单据日期"] = record.AssessmentEndDate.HasValue
            ? $"{record.AssessmentDate:yyyy-MM-dd} 至 {record.AssessmentEndDate.Value:yyyy-MM-dd}"
            : $"{record.AssessmentDate:yyyy-MM-dd}",
        ["录入日期"] = $"{record.DetailDate:yyyy-MM-dd}",
        ["部门"] = departmentName,
        ["姓名"] = record.PersonName,
        ["金额（元）"] = record.Amount.ToString("0.##", CultureInfo.InvariantCulture),
        ["事由"] = record.Reason,
        ["备注"] = record.Remark,
        ["状态"] = record.Status == Domain.Enums.AssessmentStatus.Completed ? "已完成" : "待处理",
        ["收款方式"] = paymentMethodName
    };

    public static List<FieldChange> Diff(
        IReadOnlyDictionary<string, string?> before,
        IReadOnlyDictionary<string, string?> after) =>
        after.Select(item => new FieldChange(item.Key, before.GetValueOrDefault(item.Key), item.Value))
            .Where(item => !string.Equals(item.Before, item.After, StringComparison.Ordinal))
            .ToList();

    public static AssessmentRecordChange Create(
        AssessmentRecord record,
        string action,
        long? actorUserId,
        string actorName,
        DateTime occurredAt,
        IReadOnlyList<FieldChange> changes) => new()
    {
        Record = record,
        Action = action,
        ActorUserId = actorUserId,
        ActorName = actorName,
        OccurredAt = occurredAt,
        ChangesJson = JsonSerializer.Serialize(changes, JsonOptions)
    };

    public static IReadOnlyList<FieldChange> Read(string changesJson) =>
        JsonSerializer.Deserialize<List<FieldChange>>(changesJson, JsonOptions) ?? [];
}
