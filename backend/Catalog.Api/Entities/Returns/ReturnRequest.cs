using System.Text.Json.Serialization;

namespace Catalog.Api.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<ReturnStatus>))]
public enum ReturnStatus { Requested, Approved, Rejected, PartiallyReceived, Received }

public class ReturnRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid ClientRequestId { get; set; }
    public string RequestHash { get; set; } = "";
    public string Reason { get; set; } = "";
    public ReturnStatus Status { get; set; } = ReturnStatus.Requested;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public string DecisionNote { get; set; } = "";
    public Guid Version { get; set; } = Guid.NewGuid();
    public List<ReturnRequestItem> Items { get; set; } = [];
    public List<ReturnReceipt> Receipts { get; set; } = [];
    public List<ReturnRefund> Refunds { get; set; } = [];
}

public class ReturnRequestItem
{
    public Guid ReturnRequestId { get; set; }
    public ReturnRequest ReturnRequest { get; set; } = null!;
    public int OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;
    public int Quantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public int RestockedQuantity { get; set; }
}
