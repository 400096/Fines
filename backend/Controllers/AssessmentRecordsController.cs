using FineManagement.Api.Data;
using FineManagement.Api.Domain.Entities;
using FineManagement.Api.Domain.Enums;
using FineManagement.Api.DTOs.Assessments;
using FineManagement.Api.DTOs.Common;
using FineManagement.Api.Extensions;
using FineManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FineManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/assessment-records")]
public class AssessmentRecordsController(AppDbContext db, IExcelService excelService) : ControllerBase
{
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

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AssessmentRecordResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await db.AssessmentRecords.AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.PaymentMethod)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    public async Task<ActionResult<AssessmentRecordResponse>> Create(CreateAssessmentRecordRequest request, CancellationToken cancellationToken)
    {
        var department = await db.Departments.SingleOrDefaultAsync(x => x.Id == request.DepartmentId && x.IsEnabled, cancellationToken);
        if (department is null) return BadRequest(new { message = "部门不存在或已停用" });

        var now = DateTime.UtcNow;
        var entity = new AssessmentRecord
        {
            AssessmentDate = request.AssessmentDate,
            DepartmentId = request.DepartmentId,
            Department = department,
            PersonName = request.PersonName.Trim(),
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            Status = AssessmentStatus.Pending,
            CreatedByUserId = User.GetUserId(),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.AssessmentRecords.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToResponse(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AssessmentRecordResponse>> Update(long id, UpdateAssessmentRecordRequest request, CancellationToken cancellationToken)
    {
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

        entity.AssessmentDate = request.AssessmentDate;
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
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(entity));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var entity = await db.AssessmentRecords.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound();
        db.AssessmentRecords.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

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

    [HttpPost("import/{batchId:long}/rows")]
    public async Task<ActionResult<ExcelStagedRow>> AddImportRow(
        long batchId,
        SaveAssessmentImportRowRequest request,
        CancellationToken cancellationToken)
    {
        var batch = await FindPendingBatchAsync(batchId, cancellationToken);
        if (batch is null) return NotFound(new { message = "导入批次不存在或已处理" });

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
            AssessmentDate = request.AssessmentDate,
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

        row.AssessmentDate = request.AssessmentDate;
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

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] AssessmentRecordQuery query, CancellationToken cancellationToken)
    {
        var content = await excelService.ExportAssessmentRecordsAsync(query, cancellationToken);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"考核记录_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    private static IQueryable<AssessmentRecord> ApplyFilters(IQueryable<AssessmentRecord> records, AssessmentRecordQuery query)
    {
        if (query.DateFrom.HasValue) records = records.Where(x => x.AssessmentDate >= query.DateFrom.Value);
        if (query.DateTo.HasValue) records = records.Where(x => x.AssessmentDate <= query.DateTo.Value);
        if (query.DepartmentId.HasValue) records = records.Where(x => x.DepartmentId == query.DepartmentId.Value);
        if (!string.IsNullOrWhiteSpace(query.Name)) records = records.Where(x => x.PersonName.Contains(query.Name));
        if (query.Status.HasValue) records = records.Where(x => x.Status == query.Status.Value);
        return records;
    }

    private static AssessmentRecordResponse ToResponse(AssessmentRecord x) => new(
        x.Id, x.AssessmentDate, x.DepartmentId, x.Department.Name, x.PersonName, x.Amount,
        AssessmentAmountFormatter.Format(x.Amount, x.PersonName), x.Reason, x.Remark, x.Status,
        x.PaymentMethodId, x.PaymentMethod?.Name, x.CreatedAt, x.UpdatedAt);

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
        row.AssessmentDate,
        row.DepartmentId ?? 0,
        row.DepartmentName,
        row.PersonName,
        row.Amount,
        AssessmentAmountFormatter.Format(row.Amount, row.PersonName),
        row.Reason,
        row.Remark,
        row.PaymentMethodId,
        row.PaymentMethod?.Name);
}
