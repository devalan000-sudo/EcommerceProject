namespace Catalog.Api.Entities;

/// <summary>
/// Comportamiento derivado de Order mantenido separado de su mapeo persistente.
/// </summary>
public partial class Order
{
    public bool IsPaymentComplete() => PaymentStatus == PaymentStatus.Paid;

    public bool IsFulfillmentClosed() => FulfillmentStatus is FulfillmentStatus.Delivered or FulfillmentStatus.Cancelled;

    public bool HasRefund() => RefundStatus is not null || RefundedAtUtc.HasValue;
}
