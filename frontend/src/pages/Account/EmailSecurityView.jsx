import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api/client.js';
import { updateEmail } from '../../services/account/accountService.js';
import { errorMessage } from '../../services/api/errors.js';
import { useCustomer } from '../../contexts/CustomerContext/CustomerContext.jsx';
import { useStore } from '../../contexts/StoreContext/StoreContext.jsx';

export default function EmailSecurityView() {
  const { businessName } = useStore();
  const { session, updateSession } = useCustomer();
  const [link] = useState(() => {
    const data = new URLSearchParams(location.hash.slice(1));
    return { action: data.get('action'), token: data.get('token') };
  });
  const [email, setEmail] = useState(() => new URLSearchParams(location.search).get('email') || '');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [done, setDone] = useState(false);
  const lock = useRef(false);
  const mode = link.action || new URLSearchParams(location.search).get('mode') || 'reset';
  useEffect(() => {
    if (location.hash) history.replaceState(null, '', location.pathname + location.search);
  }, []);
  async function submit(event) {
    event.preventDefault();
    if (lock.current) return;
    if (link.token && mode === 'reset' && password !== confirmation) {
      setError('Las contraseñas no coinciden.');
      return;
    }
    lock.current = true;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const path = link.token
        ? mode === 'confirm'
          ? 'confirm'
          : 'reset-password'
        : mode === 'confirm'
          ? 'resend-confirmation'
          : 'forgot-password';
      const payload = link.token ? { token: link.token, ...(mode === 'reset' ? { password } : {}) } : { email };
      const { data } = await updateEmail(api, path, payload, session);
      if (link.token && mode === 'reset') updateSession(null);
      setPassword('');
      setConfirmation('');
      setNotice(data.message);
      setDone(true);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  return (
    <main className="mx-auto max-w-lg px-5 py-12">
      <a href="/" className="text-xl font-bold">
        {businessName}
      </a>
      <h1 className="my-6 text-3xl font-semibold">
        {mode === 'confirm' ? 'Confirma tu correo' : 'Recupera tu contraseña'}
      </h1>
      {notice && (
        <p role="status" className="mb-5 rounded-xl bg-emerald-50 p-4">
          {notice}
        </p>
      )}
      {error && (
        <p role="alert" className="mb-5 rounded-xl bg-red-50 p-4 text-red-700">
          {error}
        </p>
      )}
      {!done && (
        <form onSubmit={submit} className="rounded-xl border bg-white p-5">
          <fieldset disabled={busy} className="space-y-5">
            {!link.token && (
              <>
                <p className="text-sm text-stone-500">
                  Introduce tu correo. Si corresponde, recibirás un enlace temporal para continuar.
                </p>
                <label className="block text-sm">
                  Correo electrónico
                  <input
                    required
                    type="email"
                    maxLength={254}
                    autoComplete="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="mt-2 w-full rounded-lg border p-3"
                  />
                </label>
              </>
            )}
            {link.token && mode === 'confirm' && (
              <p>Presiona el botón para confirmar que tienes acceso a este correo electrónico.</p>
            )}
            {link.token && mode === 'reset' && (
              <>
                {[
                  ['Nueva contraseña', password, setPassword],
                  ['Repite la contraseña', confirmation, setConfirmation],
                ].map(([label, value, setter]) => (
                  <label key={label} className="block text-sm">
                    {label}
                    <input
                      required
                      type="password"
                      minLength={12}
                      maxLength={256}
                      autoComplete="new-password"
                      value={value}
                      onChange={(e) => setter(e.target.value)}
                      className="mt-2 w-full rounded-lg border p-3"
                    />
                  </label>
                ))}
                <p className="text-xs text-stone-500">
                  Usa al menos 12 caracteres. Se cerrarán las sesiones anteriores de tu cuenta.
                </p>
              </>
            )}
            <button className="w-full rounded-lg bg-brand p-3 font-semibold text-white">
              {busy
                ? 'Procesando…'
                : link.token
                  ? mode === 'confirm'
                    ? 'Confirmar correo'
                    : 'Guardar contraseña'
                  : 'Solicitar enlace'}
            </button>
          </fieldset>
        </form>
      )}
      <div className="mt-6 flex flex-wrap gap-5 text-sm">
        <a href="/account" className="underline">
          Volver a mi cuenta
        </a>
        <a href={`/account/security?mode=${mode === 'confirm' ? 'confirm' : 'reset'}`} className="underline">
          Solicitar otro enlace
        </a>
      </div>
    </main>
  );
}
