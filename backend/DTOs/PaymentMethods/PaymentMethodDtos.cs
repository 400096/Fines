using System.ComponentModel.DataAnnotations;

namespace FineManagement.Api.DTOs.PaymentMethods;

public class SavePaymentMethodRequest
{
    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    [Range(0, 9999)] public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    [MaxLength(300)] public string? Description { get; set; }
}

public sealed record PaymentMethodResponse(long Id, string Name, int SortOrder, bool IsEnabled, string? Description, DateTime UpdatedAt);
