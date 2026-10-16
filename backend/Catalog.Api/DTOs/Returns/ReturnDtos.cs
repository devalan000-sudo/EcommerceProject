using System.ComponentModel.DataAnnotations;
using Catalog.Api.Entities;

namespace Catalog.Api.DTOs;

public class ReturnCreateDto
{
    public Guid RequestId { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
    [Required, MinLength(1), MaxLength(100)] public List<ReturnItemCreateDto> Items { get; set; } = [];
}
public class ReturnItemCreateDto
{
    [Range(1, int.MaxValue)] public int OrderItemId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}
public class ReturnReviewDto
{
    public Guid Version { get; set; }
    public ReturnStatus Status { get; set; }
    [Required, StringLength(1000)] public string DecisionNote { get; set; } = "";
}
public class ReturnReceiveDto
{
    public Guid RequestId { get; set; }
    public Guid Version { get; set; }
    [Required, StringLength(1000)] public string Note { get; set; } = "";
    [Required, MinLength(1), MaxLength(100)] public List<ReturnReceiveItemDto> Items { get; set; } = [];
}
public class ReturnReceiveItemDto
{
    public int OrderItemId { get; set; }
    public int ReceivedQuantity { get; set; }
    public int RestockedQuantity { get; set; }
}
public record ReturnReceiptItemDto(int OrderItemId, int ReceivedQuantity, int RestockedQuantity);
public record ReturnReceiptDto(Guid Id, DateTime CreatedAtUtc, string Note, List<ReturnReceiptItemDto> Items);
public record ReturnItemDto(int OrderItemId, string ProductName, string? Size, string? Color, string ImageUrl, int Quantity,
    int ReceivedQuantity = 0, int RestockedQuantity = 0, decimal UnitPrice = 0);
public record ReturnResponseDto(Guid Id, Guid OrderId, string OrderNumber, ReturnStatus Status, string Reason,
    DateTime CreatedAtUtc, DateTime? ReviewedAtUtc, string DecisionNote, Guid Version, List<ReturnItemDto> Items, List<ReturnReceiptDto> Receipts,
    PaymentMethod PaymentMethod, List<ReturnRefundDto> Refunds);
public record ReturnPageDto(List<ReturnResponseDto> Items, int Page, int PageSize, int TotalCount);
public record ReturnOptionsDto(bool CanRequest, string? UnavailableReason, List<ReturnItemDto> Items);
