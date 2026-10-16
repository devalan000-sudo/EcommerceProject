export function getPaymentStatus(api) {
  return api.get('/payments/status');
}

export function loadCheckoutData(api, session, signal) {
  const config = { headers: { Authorization: `Bearer ${session.token}` }, signal };
  return Promise.all([
    api.get('/customers/me', config),
    getCatalog('/products', { signal }),
    api.get('/customers/addresses', config),
  ]);
}

export function getOrder(api, orderId, headers) {
  return api.get(`/orders/${encodeURIComponent(orderId)}`, { headers });
}

export function quoteOrder(api, payload, headers) {
  return api.post('/orders/quote', payload, { headers });
}

export function createOrder(api, payload, clientRequestId, headers) {
  return api.post('/orders', { ...payload, clientRequestId }, { headers });
}

export function startOrCancelPayment(api, orderId, cancel, headers) {
  return api.post(`/payments/${orderId}/${cancel ? 'cancel' : 'session'}`, {}, { headers });
}
import { getCatalog } from '../../services/api/catalog.js';
