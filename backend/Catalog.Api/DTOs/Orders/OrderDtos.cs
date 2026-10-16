using System.ComponentModel.DataAnnotations;
using Catalog.Api.Entities;

namespace Catalog.Api.DTOs;

// La dirección estructurada se valida y se copia al pedido como instantánea.
public class OrderCreateDto : CustomerProfileDto
{
    [Required(ErrorMessage = "Completa los campos de la dirección de entrega.")]
    public DeliveryAddressDto? ShippingAddress { get; set; }
    public Guid? ClientRequestId { get; set; }
    [Required, EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod? PaymentMethod { get; set; }
    [Range(1, int.MaxValue)] public int? ShippingOptionId { get; set; }
    [Required, MinLength(1), MaxLength(50)]
    public List<OrderItemCreateDto> Items { get; set; } = [];
}

public class OrderItemCreateDto
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(1, int.MaxValue)] public int? VariantId { get; set; }
    [Range(1, 99)] public int Quantity { get; set; }
    [StringLength(200)] public string? Details { get; set; }
}

public record OrderItemResponseDto(int? ProductId, string ProductName, string ImageUrl,
    decimal UnitPrice, int Quantity, string? Details, decimal LineTotal,
    int? VariantId = null, string? Size = null, string? Color = null);
public record OrderResponseDto(Guid Id, string Number, DateTime CreatedAtUtc, int CustomerId,
    string FirstName, string LastName, string Email, string? Phone, string Address,
    string Currency, PaymentMethod PaymentMethod, PaymentStatus PaymentStatus,
    FulfillmentStatus FulfillmentStatus, decimal Subtotal, decimal? ShippingCost,
    decimal? Total, List<OrderItemResponseDto> Items, int? ShippingOptionId = null, string? ShippingName = null,
    string? Carrier = null, string? TrackingNumber = null, Guid FulfillmentVersion = default,
    DateTime? ManualPaymentConfirmedAtUtc = null, string? ManualPaymentMethod = null,
    bool CancellationPending = false, string? RefundStatus = null, DateTime? RefundedAtUtc = null, DateTime? RestockedAtUtc = null, bool RefundRestock = false);
public record OrderSummaryDto(Guid Id, string Number, DateTime CreatedAtUtc,
    PaymentMethod PaymentMethod, PaymentStatus PaymentStatus, FulfillmentStatus FulfillmentStatus,
    decimal Subtotal, decimal? Total, string Currency,
    bool CancellationPending = false, string? RefundStatus = null);
