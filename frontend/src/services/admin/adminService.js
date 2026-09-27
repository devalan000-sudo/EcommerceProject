import { adminRequest } from '../../services/api/admin.js';

export function adminLogin(api, credentials) {
  return api.post('/auth/login', credentials);
}

export function getAdminDashboard(api, session, query, signal) {
  return api.get(`/admin/dashboard?${query}`, { headers: { Authorization: `Bearer ${session.token}` }, signal });
}

export function getStoreSettings(api, signal) {
  return api.get('/store-settings', { signal });
}

export function saveStoreSettings(session, draft) {
  return adminRequest(session, 'put', '/store-settings', draft);
}

export function uploadProductImage(api, session, formData) {
  return api.post('/admin/images', formData, { headers: { Authorization: `Bearer ${session.token}` }, timeout: 75000 });
}

export function recordManualPayment(session, orderId, payload) {
  return adminRequest(session, 'post', `/admin/orders/${orderId}/manual-payment`, payload);
}

export function getEmailStatus(session) {
  return adminRequest(session, 'get', '/admin/email-status');
}

export function adminResourceRequest(session, method, path, data) {
  return adminRequest(session, method, path, data);
}

export function getProductVariants(session, productId) {
  return adminRequest(session, 'get', `/products/${productId}/variants`);
}

export function saveProductVariant(session, productId, draft) {
  return adminRequest(
    session,
    draft.id ? 'put' : 'post',
    `/products/${productId}/variants${draft.id ? `/${draft.id}` : ''}`,
    draft,
  );
}

export function createVariantTemplate(session, productId, template) {
  return adminRequest(session, 'post', `/products/${productId}/variants/template`, template);
}

export function getShippingOptions(session) {
  return adminRequest(session, 'get', '/shipping-options');
}

export function saveShippingOption(session, draft) {
  return adminRequest(session, draft.id ? 'put' : 'post', `/shipping-options${draft.id ? `/${draft.id}` : ''}`, draft);
}

export function getAdminOrders(session, query) {
  return adminRequest(session, 'get', `/admin/orders?${query}`);
}

export function getAdminOrder(session, id) {
  return adminRequest(session, 'get', `/orders/${id}`);
}

export function updateFulfillment(session, id, draft) {
  return adminRequest(session, 'put', `/admin/orders/${id}/fulfillment`, draft);
}

export function loadCatalogForAdmin(session) {
  return Promise.all([adminRequest(session, 'get', '/products'), adminRequest(session, 'get', '/categories')]);
}

export function saveCatalogResource(session, type, draft) {
  const payload =
    type === 'products' ? { ...draft, price: Number(draft.price), categoryId: Number(draft.categoryId) } : draft;
  return adminRequest(session, draft.id ? 'put' : 'post', `/${type}${draft.id ? `/${draft.id}` : ''}`, payload);
}

export function deleteCatalogResource(session, type, id) {
  return adminRequest(session, 'delete', `/${type}/${id}`);
}
