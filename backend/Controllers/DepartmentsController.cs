using FineManagement.Api.Data;
using FineManagement.Api.Domain.Entities;
using FineManagement.Api.DTOs.Departments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FineManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/departments")]
public class DepartmentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentResponse>>> GetList(
        [FromQuery] string? keyword,
        [FromQuery] bool? isEnabled,
        CancellationToken cancellationToken)
    {
        var query = db.Departments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        if (isEnabled.HasValue) query = query.Where(x => x.IsEnabled == isEnabled.Value);
        var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new DepartmentResponse(x.Id, x.Name, x.Code, x.SortOrder, x.IsEnabled, x.Manager, x.Remark, x.UpdatedAt))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<DepartmentResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await db.Departments.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new DepartmentResponse(x.Id, x.Name, x.Code, x.SortOrder, x.IsEnabled, x.Manager, x.Remark, x.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<DepartmentResponse>> Create(SaveDepartmentRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Departments.IgnoreQueryFilters().AnyAsync(x => x.Name == name || x.Code == code, cancellationToken))
            return Conflict(new { message = "部门名称或编码已存在（包括已删除数据）" });

        var now = DateTime.UtcNow;
        var entity = new Department
        {
            Name = name, Code = code, SortOrder = request.SortOrder, IsEnabled = request.IsEnabled,
            Manager = request.Manager?.Trim(), Remark = request.Remark?.Trim(), CreatedAt = now, UpdatedAt = now
        };
        db.Departments.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, response);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<DepartmentResponse>> Update(long id, SaveDepartmentRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Departments.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound();
        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Departments.IgnoreQueryFilters().AnyAsync(x => x.Id != id && (x.Name == name || x.Code == code), cancellationToken))
            return Conflict(new { message = "部门名称或编码已存在（包括已删除数据）" });

        entity.Name = name;
        entity.Code = code;
        entity.SortOrder = request.SortOrder;
        entity.IsEnabled = request.IsEnabled;
        entity.Manager = request.Manager?.Trim();
        entity.Remark = request.Remark?.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(entity));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var entity = await db.Departments.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound();
        if (await db.AssessmentRecords.AnyAsync(x => x.DepartmentId == id, cancellationToken))
            return Conflict(new { message = "该部门已被考核记录使用，不能删除；可改为停用" });
        db.Departments.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static DepartmentResponse ToResponse(Department x) =>
        new(x.Id, x.Name, x.Code, x.SortOrder, x.IsEnabled, x.Manager, x.Remark, x.UpdatedAt);
}
