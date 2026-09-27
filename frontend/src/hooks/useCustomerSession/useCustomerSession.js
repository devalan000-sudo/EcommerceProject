import { useCallback, useEffect, useState } from 'react';

const sessionKey = 'catalog-customer';

function readStoredSession() {
  try {
    const session = JSON.parse(sessionStorage.getItem(sessionKey));
    return session?.token && Date.parse(session.expiresAtUtc) > Date.now() ? session : null;
  } catch {
    return null;
  }
}

export function useCustomerSession(api) {
  const [session, setSession] = useState(readStoredSession);
  const updateSession = useCallback((value) => {
    setSession(value);
    try {
      value ? sessionStorage.setItem(sessionKey, JSON.stringify(value)) : sessionStorage.removeItem(sessionKey);
    } catch {
      // Permite una sesión en memoria si el navegador bloquea storage.
    }
  }, []);

  useEffect(() => {
    if (!session) return undefined;
    const timer = setTimeout(() => updateSession(null), Math.max(0, Date.parse(session.expiresAtUtc) - Date.now()));
    return () => clearTimeout(timer);
  }, [session, updateSession]);

  const request = useCallback(
    async (method, data) => {
      try {
        return await api.request({
          method,
          url: '/customers/me',
          data,
          headers: { Authorization: `Bearer ${session?.token}` },
        });
      } catch (error) {
        if (error.response?.status === 401 || error.response?.status === 403) updateSession(null);
        throw error;
      }
    },
    [api, session, updateSession],
  );

  return { session, updateSession, request };
}
