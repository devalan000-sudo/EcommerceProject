using Catalog.Api.Data;
using Catalog.Api.Entities;

namespace Catalog.Api.Services;

public static class AfterSalesEmails
{
    // Se guarda en la misma transacción que el cambio comercial. No envía HTTP aquí.
    // Añade un evento de correo a la cola sin enviarlo durante la transacción.
    public static void Queue(CatalogDbContext db, Order order, string kind, Guid eventId)
    {
        var key = $"aftersales:{kind}:{eventId:N}";
        if (!db.EmailOutbox.Local.Any(m => m.EventKey == key))
            db.EmailOutbox.Add(new EmailOutbox { EventKey = key, Kind = kind, OrderId = order.Id, CustomerId = order.CustomerId });
    }

    // Devuelve el asunto y la descripción asociados a un evento posventa.
    public static (string Subject, string Description)? Message(string kind) => kind switch
    {
        "ReturnApproved" => ("Tu devolución fue aprobada", "El negocio aprobó tu solicitud de devolución. Consulta su respuesta para coordinar la entrega de los artículos. Esta aprobación no confirma un reembolso."),
        "ReturnRejected" => ("Respuesta a tu solicitud de devolución", "El negocio rechazó tu solicitud de devolución. Puedes consultar el motivo y la respuesta en el detalle de tu pedido."),
        "ReturnReceived" => ("Recibimos artículos de tu devolución", "El negocio registró una recepción de artículos. Puede ser parcial: consulta las cantidades recibidas y las observaciones en tu pedido. La recepción no confirma una devolución de dinero."),
        "ReturnRefundSucceeded" => ("Reembolso de tu devolución confirmado", "Se confirmó un reembolso de artículos de tu devolución. Consulta el importe, los artículos y la forma de pago en el detalle de tu pedido. Este aviso no significa que se haya reembolsado todo el pedido ni confirma cuándo aparecerá el abono en tu cuenta."),
        _ => null
    };
}
