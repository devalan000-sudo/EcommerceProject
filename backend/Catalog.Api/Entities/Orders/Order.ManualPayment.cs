namespace Catalog.Api.Entities;

public partial class Order
{
    public Guid? ManualPaymentRequestId { get; set; }
    public string? ManualPaymentRequestHash { get; set; }
    public DateTime? ManualPaymentConfirmedAtUtc { get; set; }
    public int? ManualPaymentAdminId { get; set; }
    public string? ManualPaymentMethod { get; set; }
    public string? ManualPaymentReference { get; set; }
}
