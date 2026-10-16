import { useEffect, useState } from 'react';
import { readSession, saveSession } from '../../services/api/admin.js';

export function useAdminSession(onExpired) {
  const [session, setSession] = useState(readSession);

  function logout() {
    saveSession(null);
    setSession(null);
  }

  function establish(nextSession) {
    saveSession(nextSession);
    setSession(nextSession);
  }

  useEffect(() => {
    if (!session) return undefined;
    const timer = setTimeout(
      () => {
        logout();
        onExpired?.();
      },
      Math.max(0, Date.parse(session.expiresAtUtc) - Date.now()),
    );
    return () => clearTimeout(timer);
  }, [session]);

  return { session, establish, logout };
}
