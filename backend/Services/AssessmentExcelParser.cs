using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using FineManagement.Api.Domain.Enums;

namespace FineManagement.Api.Services;

public sealed record ParsedAssessmentRow(
    int RowNumber,
    AssessmentDateType AssessmentDateType,
    DateOnly AssessmentDate,
    DateOnly? AssessmentEndDate,
    DateOnly DetailDate,
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
    string? DetailDateText = null,
    string? DepartmentKey = null,
    string? PersonName = null,
    string? AmountText = null,
    string? Reason = null,
    string? Remark = null,
    string? PaymentMethodText = null);

/// <summary>
/// 解析 xls/xlsx/xlsm 考核模板，支持合并部门单元格、固定日期、日期区间和“X.X考核明细”分组行。
/// </summary>
public static partial class AssessmentExcelParser
{
    private sealed record ParsedAssessmentDate(
        AssessmentDateType Type,
        DateOnly Start,
        DateOnly? End);

    private sealed record DetailDateParts(int Month, int Day)
    {
        public DateOnly Resolve(int year)
        {
            try
            {
                return new DateOnly(year, Month, Day);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new InvalidDataException("考核明细日期格式不正确");
            }
        }
    }

    private sealed record HeaderMap(
        int Date,
        int Department,
        int PersonName,
        int Amount,
        int Reason,
        int? Remark,
        int? PaymentMethod);

    /// <summary>读取第一个有效工作表并返回可导入行和逐行识别错误。</summary>
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
                DetailDateParts? currentDetailDate = null;

                while (reader.Read())
                {
                    rowNumber++;
                    var values = Enumerable.Range(0, reader.FieldCount)
                        .Select(reader.GetValue)
                        .ToArray();

                    // “9.7考核明细”是后续记录的明细日期上下文，本身不作为数据行导入。
                    if (TryParseDetailDateHeader(values, out var sectionDetailDate))
                    {
                        currentDetailDate = sectionDetailDate;
                        currentDepartment = string.Empty;
                        continue;
                    }

                    if (TryCreateHeaderMap(values, out var detectedHeader))
                    {
                        header = detectedHeader;
                        continue;
                    }

                    if (header is null) continue;

                    if (values.All(IsEmpty)) continue;

                    try
                    {
                        // 模板纵向合并部门单元格时，空白单元格沿用最近一次出现的部门。
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

                        var assessmentDate = ParseAssessmentDate(GetValue(values, header.Date));
                        var detailDate = currentDetailDate?.Resolve(assessmentDate.Start.Year) ?? assessmentDate.Start;
                        rows.Add(new ParsedAssessmentRow(
                            rowNumber,
                            assessmentDate.Type,
                            assessmentDate.Start,
                            assessmentDate.End,
                            detailDate,
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
                            currentDetailDate is null ? null : $"{currentDetailDate.Month}.{currentDetailDate.Day}",
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

    private static bool TryParseDetailDateHeader(IReadOnlyList<object?> values, out DetailDateParts? detailDate)
    {
        foreach (var value in values)
        {
            var match = DetailDateHeaderRegex().Match(GetText(value));
            if (!match.Success) continue;
            detailDate = new DetailDateParts(
                int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture));
            return true;
        }

        detailDate = null;
        return false;
    }

    private static ParsedAssessmentDate ParseAssessmentDate(object? value)
    {
        // Excel 原生日期和 OA 序列值都视为固定日期。
        if (value is DateTime dateTime)
        {
            var date = DateOnly.FromDateTime(dateTime);
            return new ParsedAssessmentDate(AssessmentDateType.Fixed, date, null);
        }
        if (value is double serial)
        {
            var date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
            return new ParsedAssessmentDate(AssessmentDateType.Fixed, date, null);
        }

        var text = GetText(value).Replace('－', '-').Replace('—', '-');
        // 支持 2026/9/4-6 和 2026/9/4-9/6 两种日期区间写法。
        var rangeMatch = AssessmentDateRangeRegex().Match(text);
        if (rangeMatch.Success)
        {
            var year = int.Parse(rangeMatch.Groups["year"].Value, CultureInfo.InvariantCulture);
            var startMonth = int.Parse(rangeMatch.Groups["month"].Value, CultureInfo.InvariantCulture);
            var startDay = int.Parse(rangeMatch.Groups["startDay"].Value, CultureInfo.InvariantCulture);
            var endMonthText = rangeMatch.Groups["endMonth"].Value;
            var endMonth = string.IsNullOrWhiteSpace(endMonthText)
                ? startMonth
                : int.Parse(endMonthText, CultureInfo.InvariantCulture);
            var endDay = int.Parse(rangeMatch.Groups["endDay"].Value, CultureInfo.InvariantCulture);
            try
            {
                var start = new DateOnly(year, startMonth, startDay);
                var end = new DateOnly(year, endMonth, endDay);
                if (end < start) throw new InvalidDataException("考核日期区间的结束日期不能早于开始日期");
                return new ParsedAssessmentDate(AssessmentDateType.Range, start, end);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new InvalidDataException("考核日期格式不正确");
            }
        }

        if (AssessmentDateRangeIntentRegex().IsMatch(text))
            throw new InvalidDataException("考核日期区间格式不正确，请使用如 2026/9/4-6 的格式");

        if (DateOnly.TryParse(text, CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.None, out var fixedDate))
            return new ParsedAssessmentDate(AssessmentDateType.Fixed, fixedDate, null);
        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.None, out dateTime))
            return new ParsedAssessmentDate(AssessmentDateType.Fixed, DateOnly.FromDateTime(dateTime), null);
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

    [GeneratedRegex(@"^\s*(?<month>\d{1,2})\s*[./月]\s*(?<day>\d{1,2})\s*日?\s*考核明细\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex DetailDateHeaderRegex();

    [GeneratedRegex(@"^\s*(?<year>\d{4})\s*[/.-]\s*(?<month>\d{1,2})\s*[/.-]\s*(?<startDay>\d{1,2})\s*日?\s*[-~～至]\s*(?:(?<endMonth>\d{1,2})\s*[/.-]\s*)?(?<endDay>\d{1,2})\s*日?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex AssessmentDateRangeRegex();

    [GeneratedRegex(@"^\s*\d{4}\s*[/.-]\s*\d{1,2}\s*[/.-]\s*\d{1,2}\s*日?\s*[-~～至]", RegexOptions.CultureInvariant)]
    private static partial Regex AssessmentDateRangeIntentRegex();
}
