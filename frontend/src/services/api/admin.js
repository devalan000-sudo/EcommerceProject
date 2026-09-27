import { api } from './client.js';

// La sesión dura hasta cerrar la pestaña. Nunca adjuntar el JWT al cliente público.
export function readSession() {
  try {
    const session = JSON.parse(sessionStorage.getItem('catalog-admin-v2'));
    return session?.token && Date.parse(session.expiresAtUtc) > Date.now() ? session : null;
  } catch {
    return null;
  }
}
export function saveSession(session) {
  try {
    session
      ? sessionStorage.setItem('catalog-admin-v2', JSON.stringify(session))
      : sessionStorage.removeItem('catalog-admin-v2');
  } catch {
    /* Permite una sesión en memoria si el navegador bloquea storage. */
  }
}
export function adminRequest(session, method, path, data) {
  return api.request({ method, url: path, data, headers: { Authorization: `Bearer ${session.token}` } });
}
