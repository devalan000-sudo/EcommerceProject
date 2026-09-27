using System.Text.Json.Serialization;

namespace Catalog.Api.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))]
public enum PaymentMethod { Card, WhatsApp }
[JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))]
public enum PaymentStatus { Pending, Paid, Failed, Refunded }
[JsonConverter(typeof(JsonStringEnumConverter<FulfillmentStatus>))]
public enum FulfillmentStatus { PendingConfirmation, Preparing, Shipped, Delivered, Cancelled }

public partial class Order
{
    public Guid Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Currency { get; set; } = "MXN";
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public decimal Subtotal { get; set; }
    // null significa pendiente de cotizar, no envío gratis.
    public decimal? ShippingCost { get; set; }
    public int? ShippingOptionId { get; set; }
    public string? ShippingName { get; set; }
    public decimal? Total { get; set; }
    public string? StripeSessionId { get; set; }
    public string? StripeFrontendUrl { get; set; }
    public bool StripeHold { get; set; }
    public DateTime? PaymentDeadlineUtc { get; set; }
    public Guid? ClientRequestId { get; set; }
    public string? RequestHash { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}
