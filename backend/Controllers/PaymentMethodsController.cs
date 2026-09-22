using FineManagement.Api.Data;
using FineManagement.Api.Domain.Entities;
using FineManagement.Api.DTOs.PaymentMethods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FineManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payment-methods")]
public class PaymentMethodsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodResponse>>> GetList(
        [FromQuery] string? keyword,
        [FromQuery] bool? isEnabled,
        CancellationToken cancellationToken)
    {
        var query = db.PaymentMethods.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(x => x.Name.Contains(keyword));
        if (isEnabled.HasValue) query = query.Where(x => x.IsEnabled == isEnabled.Value);
        var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new PaymentMethodResponse(x.Id, x.Name, x.SortOrder, x.IsEnabled, x.Description, x.UpdatedAt))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<PaymentMethodResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await db.PaymentMethods.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new PaymentMethodResponse(x.Id, x.Name, x.SortOrder, x.IsEnabled, x.Description, x.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentMethodResponse>> Create(SavePaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await db.PaymentMethods.IgnoreQueryFilters().AnyAsync(x => x.Name == name, cancellationToken))
            return Conflict(new { message = "收款方式名称已存在（包括已删除数据）" });
        var now = DateTime.UtcNow;
        var entity = new PaymentMethod
        {
            Name = name, SortOrder = request.SortOrder, IsEnabled = request.IsEnabled,
            Description = request.Description?.Trim(), CreatedAt = now, UpdatedAt = now
        };
        db.PaymentMethods.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToResponse(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<PaymentMethodResponse>> Update(long id, SavePaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.PaymentMethods.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound();
        var name = request.Name.Trim();
        if (await db.PaymentMethods.IgnoreQueryFilters().AnyAsync(x => x.Id != id && x.Name == name, cancellationToken))
            return Conflict(new { message = "收款方式名称已存在（包括已删除数据）" });
        entity.Name = name;
        entity.SortOrder = request.SortOrder;
        entity.IsEnabled = request.IsEnabled;
        entity.Description = request.Description?.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(entity));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var entity = await db.PaymentMethods.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound();
        db.PaymentMethods.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static PaymentMethodResponse ToResponse(PaymentMethod x) =>
        new(x.Id, x.Name, x.SortOrder, x.IsEnabled, x.Description, x.UpdatedAt);
}
