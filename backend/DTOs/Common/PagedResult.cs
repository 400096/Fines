namespace FineManagement.Api.DTOs.Common;

/// <summary>通用分页返回模型。</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
