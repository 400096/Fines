using FineManagement.Api.Data;
using FineManagement.Api.Domain.Entities;
using FineManagement.Api.DTOs.Common;
using FineManagement.Api.DTOs.Users;
using FineManagement.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FineManagement.Api.Controllers;

[ApiController]
[Authorize(Roles = "超级管理员")]
[Route("api/users")]
public class UsersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponse>>> GetList([FromQuery] UserQuery query, CancellationToken cancellationToken)
    {
        var usersQuery = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
            usersQuery = usersQuery.Where(x => x.Username.Contains(query.Keyword) || x.Name.Contains(query.Keyword));
        if (!string.IsNullOrWhiteSpace(query.Role)) usersQuery = usersQuery.Where(x => x.Role == query.Role);
        if (query.IsEnabled.HasValue) usersQuery = usersQuery.Where(x => x.IsEnabled == query.IsEnabled.Value);

        var total = await usersQuery.CountAsync(cancellationToken);
        var items = await usersQuery.OrderBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new UserResponse(x.Id, x.Username, x.Name, x.Phone, x.Role, x.IsEnabled, x.LastLoginAt, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return Ok(new PagedResult<UserResponse>(items, total, query.Page, query.PageSize));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<UserResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new UserResponse(x.Id, x.Username, x.Name, x.Phone, x.Role, x.IsEnabled, x.LastLoginAt, x.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(x => x.Username == username, cancellationToken))
            return Conflict(new { message = "用户名已存在（包括已删除数据）" });

        var now = DateTime.UtcNow;
        var entity = new AppUser
        {
            Username = username,
            Name = request.Name.Trim(),
            Phone = request.Phone?.Trim(),
            Role = request.Role.Trim(),
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now
        };
        entity.PasswordHash = new PasswordHasher<AppUser>().HashPassword(entity, request.Password);
        db.Users.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToResponse(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<UserResponse>> Update(long id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Users.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound();
        if (entity.Role == "超级管理员" && entity.IsEnabled && (request.Role != "超级管理员" || !request.IsEnabled))
        {
            var otherAdmins = await db.Users.CountAsync(x => x.Id != id && x.Role == "超级管理员" && x.IsEnabled, cancellationToken);
            if (otherAdmins == 0) return Conflict(new { message = "系统至少需要保留一个启用的超级管理员" });
        }

        entity.Name = request.Name.Trim();
        entity.Phone = request.Phone?.Trim();
        entity.Role = request.Role.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Password))
            entity.PasswordHash = new PasswordHasher<AppUser>().HashPassword(entity, request.Password);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(entity));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() == id) return Conflict(new { message = "不能删除当前登录用户" });
        var entity = await db.Users.FindAsync([id], cancellationToken);
        if (entity is null) return NotFound();
        if (entity.Role == "超级管理员" && entity.IsEnabled)
        {
            var otherAdmins = await db.Users.CountAsync(x => x.Id != id && x.Role == "超级管理员" && x.IsEnabled, cancellationToken);
            if (otherAdmins == 0) return Conflict(new { message = "系统至少需要保留一个启用的超级管理员" });
        }
        db.Users.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static UserResponse ToResponse(AppUser x) =>
        new(x.Id, x.Username, x.Name, x.Phone, x.Role, x.IsEnabled, x.LastLoginAt, x.CreatedAt);
}
