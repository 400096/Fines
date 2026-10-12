using FineManagement.Api.Data;
using FineManagement.Api.Domain.Entities;
using FineManagement.Api.Domain.Enums;
using FineManagement.Api.DTOs.Assessments;
using FineManagement.Api.DTOs.Common;
using FineManagement.Api.Extensions;
using FineManagement.Api.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FineManagement.Api.Controllers;

/// <summary>考核记录查询、维护、Excel 暂存导入及导出接口。</summary>
[ApiController]
[Authorize]
[Route("api/assessment-records")]
public class AssessmentRecordsController(AppDbContext db, IExcelService excelService) : ControllerBase
{
    /// <summary>按日期、录入日期、部门、姓名、事由、备注和状态分页查询考核记录。</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AssessmentRecordResponse>>> GetList(
        [FromQuery] AssessmentRecordQuery query,
        CancellationToken cancellationToken)
    {
        var recordsQuery = ApplyFilters(db.AssessmentRecords.AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.PaymentMethod), query);

        var total = await recordsQuery.CountAsync(cancellationToken);
        var items = await recordsQuery
            .ApplySorting(query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<AssessmentRecordResponse>(items, total, query.Page, query.PageSize));
    }

    /// <summary>按输入内容返回最近使用过的姓名，供新增和编辑记录时自动补全。</summary>
    [HttpGet("person-name-suggestions")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetPersonNameSuggestions(
        [FromQuery] string? keyword,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeyword = keyword?.Trim();
        var take = Math.Clamp(limit, 1, 20);
        var records = db.AssessmentRecords.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            records = records.Where(x => x.PersonName.Contains(normalizedKeyword));
        }

        var names = await records
            .GroupBy(x => x.PersonName)
            .Select(group => new
            {
                Name = group.Key,
                LastUsedAt = group.Max(x => x.UpdatedAt)
            })
            .OrderByDescending(item => item.LastUsedAt)
            .ThenBy(item => item.Name)
            .Take(take)
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);

        return Ok(names);
    }

    /// <summary>获取一条考核记录的完整信息。</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<AssessmentRecordResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await db.AssessmentRecords.AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.PaymentMethod)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    /// <summary>获取只读详情和已记录的字段变更时间线；逻辑删除后仍可读取历史。</summary>
    [HttpGet("{id:long}/details")]
    public async Task<ActionResult<AssessmentRecordDetailResponse>> GetDetails(long id, CancellationToken cancellationToken)
    {
        var item = await db.AssessmentRecords.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.PaymentMethod)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return NotFound();

        var entries = await db.AssessmentRecordChanges.AsNoTracking()
            .Where(x => x.AssessmentRecordId == id)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        var timeline = entries.Select(x => new AssessmentRecordChangeResponse(
            x.Id, x.Action, x.ActorName, DateTime.SpecifyKind(x.OccurredAt, DateTimeKind.Utc),
            AssessmentChangeTracker.Read(x.ChangesJson)
                .Select(change => new AssessmentFieldChangeResponse(change.Field, change.Before, change.After))
                .ToList())).ToList();
        var hasEarlierUntrackedChanges = !entries.Any(x => x.Action is "Created" or "Imported");
        return Ok(new AssessmentRecordDetailResponse(ToResponse(item), item.IsDeleted,
            hasEarlierUntrackedChanges, timeline));
    }

    /// <summary>新增考核记录；收款方式可选，选择后记录自动变为已完成。</summary>
    [HttpPost]
    public async Task<ActionResult<AssessmentRecordResponse>> Create(CreateAssessmentRecordRequest request, CancellationToken cancellationToken)
    {
        var dateValidation = ValidateDates(request.AssessmentDateType, request.AssessmentDate, request.AssessmentEndDate, request.DetailDate);
        if (dateValidation is not null) return BadRequest(new { message = dateValidation });
        var department = await db.Departments.SingleOrDefaultAsync(x => x.Id == request.DepartmentId && x.IsEnabled, cancellationToken);
        if (department is null) return BadRequest(new { message = "部门不存在或已停用" });

        PaymentMethod? paymentMethod = null;
        if (request.PaymentMethodId.HasValue)
        {
            paymentMethod = await db.PaymentMethods.SingleOrDefaultAsync(
                x => x.Id == request.PaymentMethodId.Value && x.IsEnabled, cancellationToken);
            if (paymentMethod is null) return BadRequest(new { message = "收款方式不存在或已停用" });
        }

        var now = DateTime.UtcNow;
        var entity = new AssessmentRecord
        {
            AssessmentDateType = request.AssessmentDateType,
            AssessmentDate = request.AssessmentDate,
            AssessmentEndDate = NormalizeEndDate(request.AssessmentDateType, request.AssessmentEndDate),
            DetailDate = request.DetailDate,
            DepartmentId = request.DepartmentId,
            Department = department,
            PersonName = request.PersonName.Trim(),
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            PaymentMethodId = paymentMethod?.Id,
            PaymentMethod = paymentMethod,
            Status = paymentMethod is null ? AssessmentStatus.Pending : AssessmentStatus.Completed,
            CreatedByUserId = User.GetUserId(),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.AssessmentRecords.Add(entity);
        var createdFields = AssessmentChangeTracker.Capture(entity, department.Name, paymentMethod?.Name);
        db.AssessmentRecordChanges.Add(AssessmentChangeTracker.Create(entity, "Created", User.GetUserId(),
            CurrentActorName(), now, AssessmentChangeTracker.Diff(new Dictionary<string, string?>(), createdFields)));
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToResponse(entity));
    }

    /// <summary>编辑考核记录；选择收款方式后记录自动变为已完成。</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<AssessmentRecordResponse>> Update(long id, UpdateAssessmentRecordRequest request, CancellationToken cancellationToken)
    {
        var dateValidation = ValidateDates(request.AssessmentDateType, request.AssessmentDate, request.AssessmentEndDate, request.DetailDate);
        if (dateValidation is not null) return BadRequest(new { message = dateValidation });
        var entity = await db.AssessmentRecords
            .Include(x => x.Department)
            .Include(x => x.PaymentMethod)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return NotFound();

        var department = await db.Departments.SingleOrDefaultAsync(x => x.Id == request.DepartmentId && x.IsEnabled, cancellationToken);
        if (department is null) return BadRequest(new { message = "部门不存在或已停用" });

        PaymentMethod? paymentMethod = null;
        if (request.PaymentMethodId.HasValue)
        {
            paymentMethod = await db.PaymentMethods.SingleOrDefaultAsync(x => x.Id == request.PaymentMethodId.Value && x.IsEnabled, cancellationToken);
            if (paymentMethod is null) return BadRequest(new { message = "收款方式不存在或已停用" });
        }

        var beforeFields = AssessmentChangeTracker.Capture(entity, entity.Department.Name, entity.PaymentMethod?.Name);

        entity.AssessmentDateType = request.AssessmentDateType;
        entity.AssessmentDate = request.AssessmentDate;
        entity.AssessmentEndDate = NormalizeEndDate(request.AssessmentDateType, request.AssessmentEndDate);
        entity.DetailDate = request.DetailDate;
        entity.DepartmentId = department.Id;
        entity.Department = department;
        entity.PersonName = request.PersonName.Trim();
        entity.Amount = request.Amount;
        entity.Reason = request.Reason.Trim();
        entity.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        entity.PaymentMethodId = paymentMethod?.Id;
        entity.PaymentMethod = paymentMethod;
        entity.Status = paymentMethod is null ? AssessmentStatus.Pending : AssessmentStatus.Completed;
        entity.UpdatedAt = DateTime.UtcNow;
        var afterFields = AssessmentChangeTracker.Capture(entity, department.Name, paymentMethod?.Name);
        var changedFields = AssessmentChangeTracker.Diff(beforeFields, afterFields);
        if (changedFields.Count > 0)
            db.AssessmentRecordChanges.Add(AssessmentChangeTracker.Create(entity, "Updated", User.GetUserId(),
                CurrentActorName(), entity.UpdatedAt, changedFields));
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(entity));
    }

    /// <summary>逻辑删除考核记录。</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var entity = await db.AssessmentRecords.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return NotFound();

        // 直接更新逻辑删除字段，避免把存在必需变更日志关联的记录标记为 EF Deleted 状态。
        var deletedAt = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = deletedAt;
        entity.UpdatedAt = deletedAt;
        db.AssessmentRecordChanges.Add(AssessmentChangeTracker.Create(entity, "Deleted", User.GetUserId(),
            CurrentActorName(), deletedAt, []));
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>解析 Excel 并写入待确认中间表。</summary>
    [HttpPost("import")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ExcelImportResult>> Import(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0) return BadRequest(new { message = "请选择 Excel 文件" });
        if (!new[] { ".xls", ".xlsx", ".xlsm" }.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            return BadRequest(new { message = "仅支持 .xls、.xlsx 或 .xlsm 文件" });

        await using var stream = file.OpenReadStream();
        return Ok(await excelService.StageAssessmentRecordsAsync(stream, User.GetUserId(), cancellationToken));
    }

    /// <summary>在指定导入批次中手工添加一条待确认数据。</summary>
    [HttpPost("import/{batchId:long}/rows")]
    public async Task<ActionResult<ExcelStagedRow>> AddImportRow(
        long batchId,
        SaveAssessmentImportRowRequest request,
        CancellationToken cancellationToken)
    {
        var batch = await FindPendingBatchAsync(batchId, cancellationToken);
        if (batch is null) return NotFound(new { message = "导入批次不存在或已处理" });
        var dateValidation = ValidateDates(request.AssessmentDateType, request.AssessmentDate, request.AssessmentEndDate, request.DetailDate);
        if (dateValidation is not null) return BadRequest(new { message = dateValidation });

        Department? department = null;
        if (request.DepartmentId.HasValue && request.DepartmentId.Value > 0)
        {
            department = await db.Departments.SingleOrDefaultAsync(
                x => x.Id == request.DepartmentId.Value && x.IsEnabled, cancellationToken);
            if (department is null) return BadRequest(new { message = "部门不存在或已停用" });
        }
        var departmentName = department?.Name ?? request.DepartmentName.Trim();
        if (string.IsNullOrWhiteSpace(departmentName)) return BadRequest(new { message = "请选择或输入部门" });

        PaymentMethod? paymentMethod = null;
        if (request.PaymentMethodId.HasValue)
        {
            paymentMethod = await db.PaymentMethods.SingleOrDefaultAsync(
                x => x.Id == request.PaymentMethodId.Value && x.IsEnabled, cancellationToken);
            if (paymentMethod is null) return BadRequest(new { message = "收款方式不存在或已停用" });
        }

        var now = DateTime.UtcNow;
        var row = new AssessmentImportRow
        {
            BatchId = batch.Id,
            SourceRowNumber = 0,
            AssessmentDateType = request.AssessmentDateType,
            AssessmentDate = request.AssessmentDate,
            AssessmentEndDate = NormalizeEndDate(request.AssessmentDateType, request.AssessmentEndDate),
            DetailDate = request.DetailDate,
            DepartmentId = department?.Id,
            Department = department,
            DepartmentName = departmentName,
            PersonName = request.PersonName.Trim(),
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            PaymentMethodId = paymentMethod?.Id,
            PaymentMethod = paymentMethod,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.AssessmentImportRows.Add(row);
        batch.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToStagedRow(row));
    }

    /// <summary>修改导入批次中的一条待确认数据。</summary>
    [HttpPut("import/{batchId:long}/rows/{rowId:long}")]
    public async Task<ActionResult<ExcelStagedRow>> UpdateImportRow(
        long batchId,
        long rowId,
        SaveAssessmentImportRowRequest request,
        CancellationToken cancellationToken)
    {
        var row = await db.AssessmentImportRows
            .Include(x => x.Batch)
            .Include(x => x.Department)
            .Include(x => x.PaymentMethod)
            .SingleOrDefaultAsync(x => x.Id == rowId && x.BatchId == batchId, cancellationToken);
        if (row is null || row.Batch.Status != AssessmentImportStatus.Pending || !CanAccessBatch(row.Batch))
            return NotFound(new { message = "暂存记录不存在或导入批次已处理" });
        var dateValidation = ValidateDates(request.AssessmentDateType, request.AssessmentDate, request.AssessmentEndDate, request.DetailDate);
        if (dateValidation is not null) return BadRequest(new { message = dateValidation });

        Department? department = null;
        if (request.DepartmentId.HasValue && request.DepartmentId.Value > 0)
        {
            department = await db.Departments.SingleOrDefaultAsync(
                x => x.Id == request.DepartmentId.Value && x.IsEnabled, cancellationToken);
            if (department is null) return BadRequest(new { message = "部门不存在或已停用" });
        }
        var departmentName = department?.Name ?? request.DepartmentName.Trim();
        if (string.IsNullOrWhiteSpace(departmentName)) return BadRequest(new { message = "请选择或输入部门" });

        PaymentMethod? paymentMethod = null;
        if (request.PaymentMethodId.HasValue)
        {
            paymentMethod = await db.PaymentMethods.SingleOrDefaultAsync(
                x => x.Id == request.PaymentMethodId.Value && x.IsEnabled, cancellationToken);
            if (paymentMethod is null) return BadRequest(new { message = "收款方式不存在或已停用" });
        }

        row.AssessmentDateType = request.AssessmentDateType;
        row.AssessmentDate = request.AssessmentDate;
        row.AssessmentEndDate = NormalizeEndDate(request.AssessmentDateType, request.AssessmentEndDate);
        row.DetailDate = request.DetailDate;
        row.DepartmentId = department?.Id;
        row.Department = department;
        row.DepartmentName = departmentName;
        row.PersonName = request.PersonName.Trim();
        row.Amount = request.Amount;
        row.Reason = request.Reason.Trim();
        row.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        row.PaymentMethodId = paymentMethod?.Id;
        row.PaymentMethod = paymentMethod;
        row.UpdatedAt = DateTime.UtcNow;
        row.Batch.UpdatedAt = row.UpdatedAt;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToStagedRow(row));
    }

    /// <summary>从导入批次中逻辑删除一条待确认数据。</summary>
    [HttpDelete("import/{batchId:long}/rows/{rowId:long}")]
    public async Task<IActionResult> DeleteImportRow(long batchId, long rowId, CancellationToken cancellationToken)
    {
        var row = await db.AssessmentImportRows
            .Include(x => x.Batch)
            .SingleOrDefaultAsync(x => x.Id == rowId && x.BatchId == batchId, cancellationToken);
        if (row is null || row.Batch.Status != AssessmentImportStatus.Pending || !CanAccessBatch(row.Batch))
            return NotFound();

        db.AssessmentImportRows.Remove(row);
        row.Batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>确认导入批次，并将暂存数据一次性写入正式记录表。</summary>
    [HttpPost("import/{batchId:long}/confirm")]
    public async Task<ActionResult<ExcelImportConfirmResult>> ConfirmImport(long batchId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await excelService.ConfirmAssessmentRecordsAsync(batchId, User.GetUserId(), cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>取消并逻辑删除尚未确认的导入批次。</summary>
    [HttpDelete("import/{batchId:long}")]
    public async Task<IActionResult> CancelImport(long batchId, CancellationToken cancellationToken)
    {
        var batch = await db.AssessmentImportBatches
            .Include(x => x.Rows)
            .SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken);
        if (batch is null || batch.Status != AssessmentImportStatus.Pending || !CanAccessBatch(batch))
            return NotFound();

        batch.Status = AssessmentImportStatus.Cancelled;
        batch.UpdatedAt = DateTime.UtcNow;
        db.AssessmentImportRows.RemoveRange(batch.Rows);
        db.AssessmentImportBatches.Remove(batch);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>按当前筛选和排序条件导出全部匹配记录。</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] AssessmentRecordQuery query, CancellationToken cancellationToken)
    {
        var content = await excelService.ExportAssessmentRecordsAsync(query, cancellationToken);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"考核记录_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    private static IQueryable<AssessmentRecord> ApplyFilters(IQueryable<AssessmentRecord> records, AssessmentRecordQuery query)
    {
        // 区间记录只要与查询区间有重叠即命中；录入日期按财务登记日期筛选。
        if (query.DateFrom.HasValue) records = records.Where(x => (x.AssessmentEndDate ?? x.AssessmentDate) >= query.DateFrom.Value);
        if (query.DateTo.HasValue) records = records.Where(x => x.AssessmentDate <= query.DateTo.Value);
        if (query.DetailDateFrom.HasValue) records = records.Where(x => x.DetailDate >= query.DetailDateFrom.Value);
        if (query.DetailDateTo.HasValue) records = records.Where(x => x.DetailDate <= query.DetailDateTo.Value);
        var departmentIds = query.DepartmentIds.Where(id => id > 0).Distinct().ToArray();
        if (departmentIds.Length > 0) records = records.Where(x => departmentIds.Contains(x.DepartmentId));
        else if (query.DepartmentId.HasValue) records = records.Where(x => x.DepartmentId == query.DepartmentId.Value);
        if (!string.IsNullOrWhiteSpace(query.Name)) records = records.Where(x => x.PersonName.Contains(query.Name));
        var reason = query.Reason?.Trim();
        if (!string.IsNullOrWhiteSpace(reason)) records = records.Where(x => x.Reason.Contains(reason));
        var remark = query.Remark?.Trim();
        if (!string.IsNullOrWhiteSpace(remark)) records = records.Where(x => x.Remark != null && x.Remark.Contains(remark));
        if (query.Status.HasValue) records = records.Where(x => x.Status == query.Status.Value);
        return records;
    }

    private static AssessmentRecordResponse ToResponse(AssessmentRecord x) => new(
        x.Id, x.AssessmentDateType, x.AssessmentDate, x.AssessmentEndDate, x.DetailDate,
        x.DepartmentId, x.Department.Name, x.PersonName, x.Amount,
        AssessmentAmountFormatter.Format(x.Amount, x.PersonName), x.Reason, x.Remark, x.Status,
        x.PaymentMethodId, x.PaymentMethod?.Name,
        DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc),
        DateTime.SpecifyKind(x.UpdatedAt, DateTimeKind.Utc));

    private string CurrentActorName() =>
        User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity?.Name ?? "未知用户";

    private async Task<AssessmentImportBatch?> FindPendingBatchAsync(long batchId, CancellationToken cancellationToken)
    {
        var batch = await db.AssessmentImportBatches.SingleOrDefaultAsync(
            x => x.Id == batchId && x.Status == AssessmentImportStatus.Pending, cancellationToken);
        return batch is not null && CanAccessBatch(batch) ? batch : null;
    }

    private bool CanAccessBatch(AssessmentImportBatch batch) =>
        !batch.CreatedByUserId.HasValue || batch.CreatedByUserId == User.GetUserId();

    private static ExcelStagedRow ToStagedRow(AssessmentImportRow row) => new(
        row.Id,
        row.SourceRowNumber,
        row.AssessmentDateType,
        row.AssessmentDate,
        row.AssessmentEndDate,
        row.DetailDate,
        row.DepartmentId ?? 0,
        row.DepartmentName,
        row.PersonName,
        row.Amount,
        AssessmentAmountFormatter.Format(row.Amount, row.PersonName),
        row.Reason,
        row.Remark,
        row.PaymentMethodId,
        row.PaymentMethod?.Name);

    private static string? ValidateDates(
        AssessmentDateType type,
        DateOnly start,
        DateOnly? end,
        DateOnly detailDate)
    {
        if (!Enum.IsDefined(type)) return "单据日期类型不正确";
        if (start == default) return "请选择单据日期";
        if (detailDate == default) return "请选择录入日期";
        if (type == AssessmentDateType.Range)
        {
            if (!end.HasValue) return "请选择单据日期区间的结束日期";
            if (end.Value < start) return "单据日期区间的结束日期不能早于开始日期";
        }
        return null;
    }

    private static DateOnly? NormalizeEndDate(AssessmentDateType type, DateOnly? end) =>
        type == AssessmentDateType.Range ? end : null;
}
