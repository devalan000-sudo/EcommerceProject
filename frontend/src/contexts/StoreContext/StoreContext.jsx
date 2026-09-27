import { createContext, useContext, useEffect, useState } from 'react';
import { api } from '../../services/api/client.js';
import { defaultStoreSettings } from '../../config/business.js';
import { themeVariables } from '../../utils/storeTheme/storeTheme.js';

const StoreContext = createContext(null);

export function StoreProvider({ children }) {
  const [settings, setSettings] = useState(defaultStoreSettings);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    api
      .get('/store-settings', { signal: controller.signal })
      .then(({ data }) => {
        if (!controller.signal.aborted) setSettings({ ...defaultStoreSettings, ...data });
      })
      .catch(() => {
        if (!controller.signal.aborted) setError(true);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, []);
  useEffect(() => {
    if (document.querySelector('meta[name="seo-rendered"]') || /^\/products\//.test(location.pathname)) return;
    const label = {
      '/comprar': 'Catálogo',
      '/informacion': 'Contacto y políticas',
      '/admin': 'Administración',
      '/checkout': 'Checkout',
      '/account': 'Mi cuenta',
    }[location.pathname.replace(/\/+$/, '')];
    document.title = label ? `${label} | ${settings.name}` : settings.name;
  }, [settings.name]);
  useEffect(() => {
    for (const [name, value] of Object.entries(themeVariables(settings)))
      document.documentElement.style.setProperty(name, value);
    const icon = document.querySelector('link[rel="icon"]');
    if (icon) {
      icon.href = settings.faviconUrl || '/boutique.svg';
      icon.removeAttribute('type');
    }
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', settings.primaryColor);
  }, [settings]);
  function applySettings(value) {
    setSettings(value || defaultStoreSettings);
    setError(false);
  }
  return (
    <StoreContext.Provider value={{ settings, businessName: settings.name, loading, error, applySettings }}>
      {children}
    </StoreContext.Provider>
  );
}

export const useStore = () => useContext(StoreContext);
