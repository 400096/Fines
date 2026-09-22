using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace FineManagement.Api.Services;

public sealed record ParsedAssessmentRow(
    int RowNumber,
    DateOnly AssessmentDate,
    string DepartmentKey,
    string PersonName,
    decimal Amount,
    string Reason,
    string? Remark,
    string? PaymentMethodText);

public sealed record AssessmentExcelParseResult(
    IReadOnlyList<ParsedAssessmentRow> Rows,
    IReadOnlyList<ParsedAssessmentError> Errors);

public sealed record ParsedAssessmentError(
    int RowNumber,
    string Message,
    string? AssessmentDateText = null,
    string? DepartmentKey = null,
    string? PersonName = null,
    string? AmountText = null,
    string? Reason = null,
    string? Remark = null,
    string? PaymentMethodText = null);

public static partial class AssessmentExcelParser
{
    private sealed record HeaderMap(
        int Date,
        int Department,
        int PersonName,
        int Amount,
        int Reason,
        int? Remark,
        int? PaymentMethod);

    public static AssessmentExcelParseResult Parse(Stream stream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var rows = new List<ParsedAssessmentRow>();
        var errors = new List<ParsedAssessmentError>();

        try
        {
            using var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration
            {
                FallbackEncoding = Encoding.GetEncoding(936),
                LeaveOpen = true
            });

            do
            {
                HeaderMap? header = null;
                var rowNumber = 0;
                var currentDepartment = string.Empty;

                while (reader.Read())
                {
                    rowNumber++;
                    var values = Enumerable.Range(0, reader.FieldCount)
                        .Select(reader.GetValue)
                        .ToArray();

                    if (header is null)
                    {
                        if (TryCreateHeaderMap(values, out header)) continue;
                        continue;
                    }

                    if (values.All(IsEmpty)) continue;

                    try
                    {
                        var departmentText = GetText(GetValue(values, header.Department));
                        if (!string.IsNullOrWhiteSpace(departmentText)) currentDepartment = departmentText;
                        if (string.IsNullOrWhiteSpace(currentDepartment))
                            throw new InvalidDataException("部门不能为空，且上方没有可沿用的部门");

                        var personName = GetText(GetValue(values, header.PersonName));
                        if (string.IsNullOrWhiteSpace(personName)) throw new InvalidDataException("姓名不能为空");
                        if (personName.Length > 50) throw new InvalidDataException("姓名不能超过 50 个字符");

                        var reason = GetText(GetValue(values, header.Reason));
                        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidDataException("事由不能为空");
                        if (reason.Length > 300) throw new InvalidDataException("事由不能超过 300 个字符");

                        var remark = header.Remark.HasValue ? GetText(GetValue(values, header.Remark.Value)) : string.Empty;
                        if (remark.Length > 500) throw new InvalidDataException("备注不能超过 500 个字符");
                        var paymentMethod = header.PaymentMethod.HasValue
                            ? GetText(GetValue(values, header.PaymentMethod.Value))
                            : string.Empty;

                        rows.Add(new ParsedAssessmentRow(
                            rowNumber,
                            ParseDate(GetValue(values, header.Date)),
                            currentDepartment,
                            personName,
                            ParseAmount(GetValue(values, header.Amount)),
                            reason,
                            string.IsNullOrWhiteSpace(remark) ? null : remark,
                            string.IsNullOrWhiteSpace(paymentMethod) ? null : paymentMethod));
                    }
                    catch (Exception ex) when (ex is FormatException or InvalidDataException or OverflowException)
                    {
                        errors.Add(new ParsedAssessmentError(
                            rowNumber,
                            ex.Message,
                            GetNullableText(GetValue(values, header.Date)),
                            GetNullableText(GetValue(values, header.Department)) ?? GetNullableText(currentDepartment),
                            GetNullableText(GetValue(values, header.PersonName)),
                            GetNullableText(GetValue(values, header.Amount)),
                            GetNullableText(GetValue(values, header.Reason)),
                            header.Remark.HasValue ? GetNullableText(GetValue(values, header.Remark.Value)) : null,
                            header.PaymentMethod.HasValue ? GetNullableText(GetValue(values, header.PaymentMethod.Value)) : null));
                    }
                }

                if (header is not null) return new AssessmentExcelParseResult(rows, errors);
            } while (reader.NextResult());

            return new AssessmentExcelParseResult([], [new ParsedAssessmentError(0, "未找到包含日期、部门、姓名、金额和事由的表头行")]);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new AssessmentExcelParseResult([], [new ParsedAssessmentError(0, $"无法读取 Excel 文件：{ex.Message}")]);
        }
    }

    private static bool TryCreateHeaderMap(object?[] values, out HeaderMap? header)
    {
        var normalized = values.Select(value => NormalizeHeader(GetText(value))).ToArray();
        var date = FindColumn(normalized, "日期", "考核日期");
        var department = FindColumn(normalized, "部门", "部门名称", "部门编码");
        var personName = FindColumn(normalized, "姓名", "人员", "员工姓名");
        var amount = FindColumnByContains(normalized, "金额");
        var reason = FindColumn(normalized, "事由", "考核事由", "原因");

        if (date < 0 || department < 0 || personName < 0 || amount < 0 || reason < 0)
        {
            header = null;
            return false;
        }

        var remark = FindColumn(normalized, "备注", "说明");
        var paymentMethod = FindColumn(normalized, "收款方式", "支付方式");
        header = new HeaderMap(
            date,
            department,
            personName,
            amount,
            reason,
            remark >= 0 ? remark : null,
            paymentMethod >= 0 ? paymentMethod : null);
        return true;
    }

    private static int FindColumn(IReadOnlyList<string> headers, params string[] aliases)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (aliases.Any(alias => headers[index].Equals(alias, StringComparison.OrdinalIgnoreCase))) return index;
        }
        return -1;
    }

    private static int FindColumnByContains(IReadOnlyList<string> headers, string text)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (headers[index].Contains(text, StringComparison.OrdinalIgnoreCase)) return index;
        }
        return -1;
    }

    private static object? GetValue(IReadOnlyList<object?> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : null;

    private static bool IsEmpty(object? value) => value is null || string.IsNullOrWhiteSpace(GetText(value));

    private static string GetText(object? value) => value switch
    {
        null => string.Empty,
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
        _ => value.ToString()?.Trim() ?? string.Empty
    };

    private static string? GetNullableText(object? value)
    {
        var text = value is string existingText ? existingText.Trim() : GetText(value);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string NormalizeHeader(string value) => WhitespaceRegex().Replace(value, string.Empty);

    private static DateOnly ParseDate(object? value)
    {
        if (value is DateTime dateTime) return DateOnly.FromDateTime(dateTime);
        if (value is double serial) return DateOnly.FromDateTime(DateTime.FromOADate(serial));
        var text = GetText(value);
        if (DateOnly.TryParse(text, CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.None, out var date)) return date;
        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.None, out dateTime))
            return DateOnly.FromDateTime(dateTime);
        throw new InvalidDataException("考核日期格式不正确");
    }

    private static decimal ParseAmount(object? value)
    {
        if (value is not null && value is not string)
        {
            try
            {
                var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                if (number > 0) return number;
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                // 继续尝试从显示文本中提取数字。
            }
        }

        var match = AmountRegex().Match(GetText(value).Replace(',', '.'));
        if (match.Success && decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0)
            return amount;
        throw new InvalidDataException("金额必须是大于 0 的数字");
    }

    [GeneratedRegex(@"[\s　]+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex AmountRegex();
}
