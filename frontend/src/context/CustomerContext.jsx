import { createContext, useContext, useEffect, useState } from 'react';
import { api } from '../api/client.js';

const CustomerContext = createContext(null);
const sessionKey = 'catalog-customer';
function readSession() {
  try {
    const session = JSON.parse(sessionStorage.getItem(sessionKey));
    return session?.token && Date.parse(session.expiresAtUtc) > Date.now() ? session : null;
  } catch { return null; }
}
export function CustomerProvider({ children }) {
  const [session, setSession] = useState(readSession);
  function updateSession(value) {
    setSession(value);
    try { value ? sessionStorage.setItem(sessionKey, JSON.stringify(value)) : sessionStorage.removeItem(sessionKey); } catch { /* Sesión en memoria. */ }
  }
  useEffect(() => {
    if (!session) return;
    const timer = setTimeout(() => updateSession(null), Math.max(0, Date.parse(session.expiresAtUtc) - Date.now()));
    return () => clearTimeout(timer);
  }, [session]);
  async function request(method, data) {
    try { return await api.request({ method, url: '/customers/me', data, headers: { Authorization: `Bearer ${session?.token}` } }); }
    catch (error) { if (error.response?.status === 401 || error.response?.status === 403) updateSession(null); throw error; }
  }
  return <CustomerContext.Provider value={{ session, updateSession, request }}>{children}</CustomerContext.Provider>;
}
export const useCustomer = () => useContext(CustomerContext);
