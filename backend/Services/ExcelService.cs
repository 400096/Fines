using ClosedXML.Excel;
using FineManagement.Api.Data;
using FineManagement.Api.Domain.Entities;
using FineManagement.Api.Domain.Enums;
using FineManagement.Api.DTOs.Assessments;
using FineManagement.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace FineManagement.Api.Services;

/// <summary>
/// 处理 Excel 导入和导出。导入先写入中间表供前端校对，确认后再转入正式记录表。
/// </summary>
public sealed class ExcelService(AppDbContext db) : IExcelService
{
    /// <summary>解析上传文件并创建待确认导入批次，不直接写入正式考核记录。</summary>
    public async Task<ExcelImportResult> StageAssessmentRecordsAsync(Stream stream, long? userId, CancellationToken cancellationToken)
    {
        var parseResult = AssessmentExcelParser.Parse(stream);
        var now = DateTime.UtcNow;
        var batch = new AssessmentImportBatch
        {
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.AssessmentImportBatches.Add(batch);

        var departments = await db.Departments.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var departmentMap = new Dictionary<string, Department>(StringComparer.OrdinalIgnoreCase);
        foreach (var department in departments)
        {
            AddDepartmentKey(departmentMap, department.Code, department);
            AddDepartmentKey(departmentMap, department.Name, department);
            if (department.Name.EndsWith('部'))
                AddDepartmentKey(departmentMap, department.Name[..^1], department);
        }
        var paymentMethods = await db.PaymentMethods.AsNoTracking()
            .ToListAsync(cancellationToken);

        var errors = parseResult.Errors.Select(FormatImportError).ToList();
        var pendingDepartments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stagedRecords = new List<(ParsedAssessmentRow Source, AssessmentImportRow Record, PaymentMethod? PaymentMethod)>();
        var failedRows = parseResult.Errors.Select(ToFailedRow).ToList();
        // 部门按名称或编码匹配；未匹配部门保留原文，待确认导入时自动创建。
        foreach (var row in parseResult.Rows)
        {
            try
            {
                var departmentKey = NormalizeBusinessKey(row.DepartmentKey);
                departmentMap.TryGetValue(departmentKey, out var department);
                if (department is null || department.IsDeleted)
                    pendingDepartments.Add(department?.Name ?? row.DepartmentKey.Trim());

                var paymentText = row.PaymentMethodText ?? row.Remark;
                var paymentMethod = string.IsNullOrWhiteSpace(paymentText)
                    ? null
                    : paymentMethods
                        .OrderByDescending(x => x.Name.Length)
                        .FirstOrDefault(x => paymentText.Contains(x.Name, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(row.PaymentMethodText) && paymentMethod is null)
                    throw new InvalidDataException($"收款方式不存在：{row.PaymentMethodText}");

                var record = new AssessmentImportRow
                {
                    Batch = batch,
                    SourceRowNumber = row.RowNumber,
                    AssessmentDateType = row.AssessmentDateType,
                    AssessmentDate = row.AssessmentDate,
                    AssessmentEndDate = row.AssessmentEndDate,
                    DetailDate = row.DetailDate,
                    DepartmentId = department is { IsDeleted: false } ? department.Id : null,
                    DepartmentName = department?.Name ?? row.DepartmentKey.Trim(),
                    PersonName = row.PersonName,
                    Amount = row.Amount,
                    Reason = row.Reason,
                    Remark = row.Remark,
                    PaymentMethodId = paymentMethod?.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.AssessmentImportRows.Add(record);
                stagedRecords.Add((row, record, paymentMethod));
            }
            catch (Exception ex)
            {
                errors.Add($"第 {row.RowNumber} 行：{ex.Message}");
                failedRows.Add(new ExcelImportFailedRow(
                    row.RowNumber,
                    ex.Message,
                    FormatAssessmentDate(row.AssessmentDate, row.AssessmentEndDate),
                    row.DetailDate.ToString("yyyy-MM-dd"),
                    row.DepartmentKey,
                    row.PersonName,
                    AssessmentAmountFormatter.Format(row.Amount, row.PersonName),
                    row.Reason,
                    row.Remark,
                    row.PaymentMethodText));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        var stagedRows = stagedRecords.Select(item => new ExcelStagedRow(
            item.Record.Id,
            item.Source.RowNumber,
            item.Source.AssessmentDateType,
            item.Source.AssessmentDate,
            item.Source.AssessmentEndDate,
            item.Source.DetailDate,
            item.Record.DepartmentId ?? 0,
            item.Record.DepartmentName,
            item.Source.PersonName,
            item.Source.Amount,
            AssessmentAmountFormatter.Format(item.Source.Amount, item.Source.PersonName),
            item.Source.Reason,
            item.Source.Remark,
            item.PaymentMethod?.Id,
            item.PaymentMethod?.Name)).ToList();
        return new ExcelImportResult(
            batch.Id,
            stagedRows.Count,
            errors.Count,
            errors,
            pendingDepartments.ToList(),
            stagedRows,
            failedRows);
    }

    public async Task<ExcelImportConfirmResult> ConfirmAssessmentRecordsAsync(
        long batchId,
        long? userId,
        CancellationToken cancellationToken)
    {
        // 部门恢复/创建与正式记录写入位于同一事务，任一失败都会整体回滚。
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await db.AssessmentImportBatches
            .Include(x => x.Rows)
            .SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new KeyNotFoundException("导入批次不存在或已取消");

        if (batch.CreatedByUserId.HasValue && batch.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("无权确认该导入批次");
        if (batch.Status != AssessmentImportStatus.Pending)
            throw new InvalidOperationException("该导入批次已处理，请勿重复确认");
        if (batch.Rows.Count == 0)
            throw new InvalidOperationException("没有可导入的数据");

        var departments = await db.Departments.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var departmentMap = new Dictionary<string, Department>(StringComparer.OrdinalIgnoreCase);
        var departmentCodes = departments.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextDepartmentSortOrder = departments.Count == 0 ? 1 : departments.Max(x => x.SortOrder) + 1;
        foreach (var department in departments)
        {
            AddDepartmentKey(departmentMap, department.Code, department);
            AddDepartmentKey(departmentMap, department.Name, department);
            if (department.Name.EndsWith('部')) AddDepartmentKey(departmentMap, department.Name[..^1], department);
        }

        var createdDepartments = new List<string>();
        var confirmedAt = DateTime.UtcNow;
        var actorName = userId.HasValue
            ? await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Id == userId.Value)
                .Select(x => x.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? "未知用户"
            : "未知用户";
        var paymentMethodNames = await db.PaymentMethods.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        foreach (var row in batch.Rows)
        {
            Department? department = null;
            if (row.DepartmentId.HasValue)
                department = departments.FirstOrDefault(x => x.Id == row.DepartmentId.Value);

            var departmentKey = NormalizeBusinessKey(row.DepartmentName);
            if (department is null) departmentMap.TryGetValue(departmentKey, out department);

            if (department is null)
            {
                department = new Department
                {
                    Name = row.DepartmentName.Trim(),
                    Code = CreateDepartmentCode(departmentKey, departmentCodes),
                    SortOrder = nextDepartmentSortOrder++,
                    IsEnabled = true,
                    Remark = "Excel 导入确认时自动创建",
                    CreatedAt = confirmedAt,
                    UpdatedAt = confirmedAt
                };
                db.Departments.Add(department);
                departments.Add(department);
                AddDepartmentKey(departmentMap, department.Code, department);
                AddDepartmentKey(departmentMap, department.Name, department);
                if (department.Name.EndsWith('部')) AddDepartmentKey(departmentMap, department.Name[..^1], department);
                createdDepartments.Add(department.Name);
            }
            else if (department.IsDeleted)
            {
                department.IsDeleted = false;
                department.DeletedAt = null;
                department.IsEnabled = true;
                department.UpdatedAt = confirmedAt;
                createdDepartments.Add($"{department.Name}（已恢复）");
            }

            var record = new AssessmentRecord
            {
                AssessmentDateType = row.AssessmentDateType,
                AssessmentDate = row.AssessmentDate,
                AssessmentEndDate = row.AssessmentEndDate,
                DetailDate = row.DetailDate,
                Department = department,
                PersonName = row.PersonName,
                Amount = row.Amount,
                Reason = row.Reason,
                Remark = row.Remark,
                PaymentMethodId = row.PaymentMethodId,
                Status = row.PaymentMethodId.HasValue ? AssessmentStatus.Completed : AssessmentStatus.Pending,
                CreatedByUserId = userId,
                CreatedAt = confirmedAt,
                UpdatedAt = confirmedAt
            };
            db.AssessmentRecords.Add(record);
            var paymentMethodName = row.PaymentMethodId.HasValue &&
                paymentMethodNames.TryGetValue(row.PaymentMethodId.Value, out var name) ? name : null;
            var fields = AssessmentChangeTracker.Capture(record, department.Name, paymentMethodName);
            db.AssessmentRecordChanges.Add(AssessmentChangeTracker.Create(record, "Imported", userId,
                actorName, confirmedAt, AssessmentChangeTracker.Diff(new Dictionary<string, string?>(), fields)));
        }

        batch.Status = AssessmentImportStatus.Confirmed;
        batch.ConfirmedAt = confirmedAt;
        batch.UpdatedAt = confirmedAt;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ExcelImportConfirmResult(batch.Rows.Count, createdDepartments);
    }

    private static string FormatImportError(ParsedAssessmentError error) =>
        error.RowNumber > 0 ? $"第 {error.RowNumber} 行：{error.Message}" : error.Message;

    private static ExcelImportFailedRow ToFailedRow(ParsedAssessmentError error) => new(
        error.RowNumber,
        error.Message,
        error.AssessmentDateText,
        error.DetailDateText,
        error.DepartmentKey,
        error.PersonName,
        error.AmountText,
        error.Reason,
        error.Remark,
        error.PaymentMethodText);

    private static void AddDepartmentKey(IDictionary<string, Department> map, string key, Department department)
    {
        var normalized = NormalizeBusinessKey(key);
        if (!string.IsNullOrWhiteSpace(normalized) && !map.ContainsKey(normalized)) map[normalized] = department;
    }

    private static string NormalizeBusinessKey(string value) =>
        new(value.Where(character => !char.IsWhiteSpace(character)).ToArray());

    private static string CreateDepartmentCode(string departmentKey, ISet<string> existingCodes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(departmentKey)))[..16];
        var baseCode = $"IMP-{hash}";
        var code = baseCode;
        var suffix = 1;
        while (!existingCodes.Add(code))
            code = $"{baseCode}-{suffix++}";
        return code;
    }

    /// <summary>按当前全部筛选和排序条件导出考核记录。</summary>
    public async Task<byte[]> ExportAssessmentRecordsAsync(AssessmentRecordQuery query, CancellationToken cancellationToken)
    {
        var recordsQuery = db.AssessmentRecords.AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.PaymentMethod)
            .AsQueryable();

        if (query.DateFrom.HasValue) recordsQuery = recordsQuery.Where(x => (x.AssessmentEndDate ?? x.AssessmentDate) >= query.DateFrom.Value);
        if (query.DateTo.HasValue) recordsQuery = recordsQuery.Where(x => x.AssessmentDate <= query.DateTo.Value);
        if (query.DetailDateFrom.HasValue) recordsQuery = recordsQuery.Where(x => x.DetailDate >= query.DetailDateFrom.Value);
        if (query.DetailDateTo.HasValue) recordsQuery = recordsQuery.Where(x => x.DetailDate <= query.DetailDateTo.Value);
        var departmentIds = query.DepartmentIds.Where(id => id > 0).Distinct().ToArray();
        if (departmentIds.Length > 0) recordsQuery = recordsQuery.Where(x => departmentIds.Contains(x.DepartmentId));
        else if (query.DepartmentId.HasValue) recordsQuery = recordsQuery.Where(x => x.DepartmentId == query.DepartmentId.Value);
        if (!string.IsNullOrWhiteSpace(query.Name)) recordsQuery = recordsQuery.Where(x => x.PersonName.Contains(query.Name));
        var reason = query.Reason?.Trim();
        if (!string.IsNullOrWhiteSpace(reason)) recordsQuery = recordsQuery.Where(x => x.Reason.Contains(reason));
        var remark = query.Remark?.Trim();
        if (!string.IsNullOrWhiteSpace(remark)) recordsQuery = recordsQuery.Where(x => x.Remark != null && x.Remark.Contains(remark));
        if (query.Status.HasValue) recordsQuery = recordsQuery.Where(x => x.Status == query.Status.Value);

        var records = await recordsQuery
            .ApplySorting(query)
            .ToListAsync(cancellationToken);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("考核记录");
        var headers = new[] { "日期", "部门", "姓名", "金额/元", "事由", "备注" };
        var filteredDetailDate = GetSingleDetailDateFilter(query);
        var headerRow = filteredDetailDate.HasValue ? 2 : 1;
        var firstDataRow = headerRow + 1;
        for (var i = 0; i < headers.Length; i++) worksheet.Cell(headerRow, i + 1).Value = headers[i];

        for (var i = 0; i < records.Count; i++)
        {
            var row = i + firstDataRow;
            var item = records[i];
            if (item.AssessmentEndDate.HasValue)
                worksheet.Cell(row, 1).Value = FormatAssessmentDate(item.AssessmentDate, item.AssessmentEndDate);
            else
            {
                worksheet.Cell(row, 1).Value = item.AssessmentDate.ToDateTime(TimeOnly.MinValue);
                worksheet.Cell(row, 1).Style.DateFormat.Format = "yyyy/m/d";
            }
            worksheet.Cell(row, 2).Value = item.Department.Name;
            worksheet.Cell(row, 3).Value = item.PersonName;
            if (AssessmentAmountFormatter.SplitPersonNames(item.PersonName).Count > 1)
                worksheet.Cell(row, 4).Value = AssessmentAmountFormatter.Format(item.Amount, item.PersonName);
            else
                worksheet.Cell(row, 4).Value = item.Amount;
            worksheet.Cell(row, 5).Value = item.Reason;
            worksheet.Cell(row, 6).Value = BuildExportRemark(item.Remark, item.PaymentMethod?.Name);
        }

        var lastUsedRow = records.Count > 0 ? firstDataRow + records.Count - 1 : headerRow;
        var usedRange = worksheet.Range(1, 1, lastUsedRow, headers.Length);
        usedRange.Style.Font.FontName = "华文楷体";
        usedRange.Style.Font.FontSize = 11;
        usedRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.OutsideBorderColor = XLColor.Black;
        usedRange.Style.Border.InsideBorderColor = XLColor.Black;

        var headerRange = worksheet.Range(headerRow, 1, headerRow, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontSize = 12;
        headerRange.Style.Fill.BackgroundColor = XLColor.White;
        worksheet.Row(headerRow).Height = 28;
        if (records.Count > 0)
            worksheet.Rows(firstDataRow, firstDataRow + records.Count - 1).Height = 24;

        // 单日明细筛选时，按业务模板增加“X.X考核明细”标题行。
        if (filteredDetailDate.HasValue)
        {
            var titleRowRange = worksheet.Range(1, 1, 1, headers.Length);
            titleRowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2F3EA");
            worksheet.Range(1, 2, 1, headers.Length).Merge();
            worksheet.Cell(1, 2).Value = $"{filteredDetailDate.Value.Month}.{filteredDetailDate.Value.Day}考核明细";
            var titleRange = worksheet.Range(1, 2, 1, headers.Length);
            titleRange.Style.Font.FontName = "华文楷体";
            titleRange.Style.Font.FontSize = 9;
            titleRange.Style.Font.Bold = true;
            titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            worksheet.Row(1).Height = 17;
        }

        worksheet.Column(1).Width = 20;
        worksheet.Column(2).Width = 18;
        worksheet.Column(3).Width = 32;
        worksheet.Column(4).Width = 20;
        worksheet.Column(5).Width = 42;
        worksheet.Column(6).Width = 24;
        worksheet.Columns(3, 6).Style.Alignment.WrapText = true;
        worksheet.SheetView.FreezeRows(headerRow);
        worksheet.ShowGridLines = true;
        worksheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        worksheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        worksheet.PageSetup.PagesWide = 1;
        worksheet.PageSetup.PagesTall = 0;
        worksheet.PageSetup.CenterHorizontally = true;

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static string? BuildExportRemark(string? remark, string? paymentMethodName)
    {
        if (string.IsNullOrWhiteSpace(paymentMethodName)) return remark;
        if (string.IsNullOrWhiteSpace(remark)) return $"{paymentMethodName}已收";
        if (remark.Contains(paymentMethodName, StringComparison.OrdinalIgnoreCase)) return remark;
        return $"{remark}；{paymentMethodName}已收";
    }

    private static string FormatAssessmentDate(DateOnly start, DateOnly? end) =>
        end.HasValue
            ? $"{start:yyyy/M/d}-{(start.Year == end.Value.Year && start.Month == end.Value.Month ? end.Value.Day.ToString() : end.Value.ToString("M/d"))}"
            : start.ToString("yyyy/M/d");

    private static DateOnly? GetSingleDetailDateFilter(AssessmentRecordQuery query)
    {
        if (query.DetailDateFrom.HasValue && query.DetailDateTo.HasValue)
            return query.DetailDateFrom.Value == query.DetailDateTo.Value ? query.DetailDateFrom.Value : null;
        return query.DetailDateFrom ?? query.DetailDateTo;
    }
}
