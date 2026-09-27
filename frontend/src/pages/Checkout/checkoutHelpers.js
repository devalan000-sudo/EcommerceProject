import { isOrderId } from '../../utils/orders/orders.js';

export function buildCheckoutPayload(delivery, method, items) {
  return {
    ...delivery,
    phone: delivery.phone.trim() || null,
    paymentMethod: method,
    shippingOptionId: null,
    items: items.map((item) => ({
      productId: item.id,
      variantId: item.variantId || null,
      quantity: item.quantity,
      details: item.details || null,
    })),
  };
}

export function authorizationHeaders(session) {
  return { Authorization: `Bearer ${session?.token}` };
}

export function validateStripeCheckoutUrl(value) {
  const url = new URL(value);
  if (url.protocol !== 'https:' || url.hostname !== 'checkout.stripe.com') {
    throw new Error('Dirección de pago inesperada.');
  }
  return url;
}

export function checkoutLoginUrl(orderId) {
  return `/account?returnTo=checkout${isOrderId(orderId) ? `&order=${encodeURIComponent(orderId)}` : ''}`;
}
