namespace Catalog.Api.Entities;

public partial class Order
{
    public Guid? RefundRequestId { get; set; }
    public string? RefundRequestHash { get; set; }
    public string? RefundReason { get; set; }
    public DateTime? RefundRequestedAtUtc { get; set; }
    public int? RefundAdminId { get; set; }
    public string? RefundStatus { get; set; }
    public string? StripeRefundId { get; set; }
    public string? RefundPaymentIntentId { get; set; }
    public bool RefundRestock { get; set; }
    public DateTime? RefundedAtUtc { get; set; }
    public DateTime? RestockedAtUtc { get; set; }
}
