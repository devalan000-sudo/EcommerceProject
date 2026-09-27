namespace Catalog.Api.Entities;

public partial class Order
{
    public FulfillmentStatus FulfillmentStatus { get; set; } = FulfillmentStatus.PendingConfirmation;
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public Guid FulfillmentVersion { get; set; }
}
