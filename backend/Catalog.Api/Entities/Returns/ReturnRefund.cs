using System.Text.Json.Serialization;

namespace Catalog.Api.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<ReturnRefundStatus>))]
public enum ReturnRefundStatus { Pending, Succeeded, Failed, NeedsReview }

public class ReturnRefund
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReturnRequestId { get; set; }
    public ReturnRequest ReturnRequest { get; set; } = null!;
    public Guid ClientRequestId { get; set; }
    public string RequestHash { get; set; } = "";
    public int AdminId { get; set; }
    public decimal Amount { get; set; }
    public string Note { get; set; } = "";
    public string ManualReference { get; set; } = "";
    public ReturnRefundStatus Status { get; set; } = ReturnRefundStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public string? PaymentIntentId { get; set; }
    public string? StripeRefundId { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public List<ReturnRefundItem> Items { get; set; } = [];
}
public class ReturnRefundItem
{
    public Guid ReturnRefundId { get; set; }
    public ReturnRefund ReturnRefund { get; set; } = null!;
    public int OrderItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
