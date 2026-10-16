export function customerHeaders(session) {
  return { Authorization: `Bearer ${session?.token}` };
}

export function getAddresses(api, session, signal) {
  return api.get('/customers/addresses', { headers: customerHeaders(session), signal });
}

export function updateEmail(api, path, payload, session) {
  return api.post(`/customers/email/${path}`, payload, { headers: customerHeaders(session) });
}

export function authenticateCustomer(api, mode, payload) {
  return api.post(`/customers/${mode}`, payload);
}
