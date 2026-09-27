export const paymentLabels = {
  Pending: 'Pendiente',
  Paid: 'Pagado',
  Failed: 'Fallido o vencido',
  Refunded: 'Reembolsado',
};
export const deliveryLabels = {
  PendingConfirmation: 'Pendiente de confirmación',
  Preparing: 'Preparando',
  Shipped: 'Enviado',
  Delivered: 'Entregado',
  Cancelled: 'Cancelado',
};
export const paymentMethodLabels = { Card: 'Tarjeta · Stripe', WhatsApp: 'WhatsApp' };
export const isOrderId = (value) =>
  typeof value === 'string' && /^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(value);
export const orderCheckoutUrl = (id) => (isOrderId(id) ? `/checkout?order=${encodeURIComponent(id)}` : '/checkout');
export const canResumePayment = (order) =>
  order?.paymentMethod === 'Card' &&
  order.paymentStatus === 'Pending' &&
  order.fulfillmentStatus === 'PendingConfirmation' &&
  !order.cancellationPending &&
  !order.refundStatus &&
  isOrderId(order.id);
export const refundLabels = {
  Pending: 'Pendiente de confirmación',
  Succeeded: 'Confirmado',
  Failed: 'Fallido: requiere revisión',
  NeedsReview: 'Requiere revisión con Stripe',
};
export function resolutionAction(order) {
  if (
    !order ||
    order.cancellationPending ||
    order.refundStatus ||
    !['PendingConfirmation', 'Preparing'].includes(order.fulfillmentStatus)
  )
    return null;
  if (order.paymentStatus === 'Paid' && order.total > 0) return 'refund';
  if (['Pending', 'Failed'].includes(order.paymentStatus)) return 'cancel';
  return null;
}
export function formatOrderDate(value) {
  // SQL devuelve UTC sin sufijo; no interpretarlo como hora local del navegador.
  const date = new Date(/(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime())
    ? 'Fecha no disponible'
    : new Intl.DateTimeFormat('es-MX', { dateStyle: 'medium', timeStyle: 'short' }).format(date);
}
export function orderContactLink(order, businessPhone) {
  const phone = String(businessPhone ?? '').replace(/[\s()+-]/g, '');
  if (!/^[1-9]\d{7,14}$/.test(phone)) throw new Error('El negocio aún no tiene un número de WhatsApp válido.');
  const message = `Hola, quisiera consultar mi pedido ${order.number}. ¿Me ayudan con su seguimiento?`;
  return `https://wa.me/${phone}?text=${encodeURIComponent(message)}`;
}
