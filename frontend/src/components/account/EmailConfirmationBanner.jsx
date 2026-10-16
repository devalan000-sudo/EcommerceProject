import { useEffect, useState } from 'react';
import { api } from '../../services/api/client.js';
import { errorMessage } from '../../services/api/errors.js';

export default function EmailConfirmationBanner({ email, confirmed }) {
  const [settings, setSettings] = useState(null);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    api
      .get('/customers/email/status')
      .then((r) => {
        if (active) setSettings(r.data);
      })
      .catch(() => {});
    return () => {
      active = false;
    };
  }, []);
  async function resend() {
    setBusy(true);
    setNotice('');
    setError('');
    try {
      const { data } = await api.post('/customers/email/resend-confirmation', { email });
      setNotice(data.message);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="mt-5 rounded-xl bg-stone-50 p-4 text-sm">
      <p className="font-semibold">{confirmed ? 'Correo confirmado' : 'Correo pendiente de confirmación'}</p>
      {!confirmed && (
        <>
          <p className="mt-2">
            Confirma tu correo para recibir avisos de tus pedidos.
            {settings?.requireConfirmedForCheckout && ' También es necesario para finalizar compras.'}
          </p>
          <button disabled={busy || !settings?.enabled} onClick={resend} className="mt-3 underline disabled:opacity-40">
            {busy ? 'Solicitando…' : 'Enviar enlace de confirmación'}
          </button>
          {settings && !settings.enabled && (
            <p className="mt-2 text-xs text-stone-500">El negocio todavía no ha habilitado el servicio de correo.</p>
          )}
        </>
      )}
      {notice && (
        <p role="status" className="mt-3">
          {notice}
        </p>
      )}
      {error && (
        <p role="alert" className="mt-3 text-red-700">
          {error}
        </p>
      )}
    </section>
  );
}
