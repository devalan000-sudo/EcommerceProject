import { customerHeaders } from '../account/accountService.js';

export function getCustomerOrders(api, session, page, orderId, signal) {
  const path = orderId ? `/orders/${encodeURIComponent(orderId)}` : `/orders?page=${page}`;
  return api.get(path, { headers: customerHeaders(session), signal });
}

export function getCustomerOrder(api, session, orderId) {
  return api.get(`/orders/${encodeURIComponent(orderId)}`, { headers: customerHeaders(session) });
}

export function listReturnRequests(api, session, { orderId, admin, page, status, signal }) {
  const path = admin
    ? `/admin/returns?page=${page}${status ? `&status=${status}` : ''}`
    : `/returns?orderId=${orderId}&page=${page}`;
  return api.get(path, { headers: customerHeaders(session), signal });
}

export function getReturnOptions(api, session, orderId, signal) {
  return api.get(`/orders/${orderId}/return-options`, { headers: customerHeaders(session), signal });
}

export function createReturn(api, session, orderId, payload) {
  return api.post(`/orders/${orderId}/returns`, payload, { headers: customerHeaders(session) });
}

export function reviewReturn(api, session, requestId, payload) {
  return api.put(`/admin/returns/${requestId}`, payload, { headers: customerHeaders(session) });
}

export function registerReturnReceipt(api, session, requestId, payload) {
  return api.post(`/admin/returns/${requestId}/receipts`, payload, { headers: customerHeaders(session) });
}

export function createReturnRefund(api, session, requestId, payload) {
  return api.post(`/admin/returns/${requestId}/refunds`, payload, { headers: customerHeaders(session) });
}

export function resolveOrder(api, session, orderId, operation, payload) {
  return api.post(`/admin/orders/${orderId}/${operation}`, payload, { headers: customerHeaders(session) });
}
