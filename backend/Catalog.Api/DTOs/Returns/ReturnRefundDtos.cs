using System.ComponentModel.DataAnnotations;
using Catalog.Api.Entities;

namespace Catalog.Api.DTOs;

public class ReturnRefundCreateDto
{
    public Guid RequestId { get; set; }
    public Guid Version { get; set; }
    [Required, StringLength(1000)] public string Note { get; set; } = "";
    [StringLength(200)] public string ManualReference { get; set; } = "";
    public bool ManualRefundConfirmed { get; set; }
    public decimal ExpectedAmount { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<ReturnItemCreateDto> Items { get; set; } = [];
}
public class ReturnRefundRefreshDto
{
    [StringLength(255), RegularExpression("^re_[a-zA-Z0-9]+$")]
    public string? StripeRefundId { get; set; }
}
public record ReturnRefundItemDto(int OrderItemId, int Quantity, decimal UnitPrice);
public record ReturnRefundDto(Guid Id, decimal Amount, ReturnRefundStatus Status, string Note,
    DateTime CreatedAtUtc, DateTime? CompletedAtUtc, List<ReturnRefundItemDto> Items);
