namespace Catalog.Api.Entities;

public class ReturnReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReturnRequestId { get; set; }
    public ReturnRequest ReturnRequest { get; set; } = null!;
    public Guid ClientRequestId { get; set; }
    public string RequestHash { get; set; } = "";
    public int AdminId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Note { get; set; } = "";
    public List<ReturnReceiptItem> Items { get; set; } = [];
}

public class ReturnReceiptItem
{
    public Guid ReturnReceiptId { get; set; }
    public ReturnReceipt ReturnReceipt { get; set; } = null!;
    public int OrderItemId { get; set; }
    public int ReceivedQuantity { get; set; }
    public int RestockedQuantity { get; set; }
}
