import CustomerAddresses from './CustomerAddresses.jsx';
import { useStore } from '../../contexts/StoreContext/StoreContext.jsx';
import { useCallback, useEffect, useState } from 'react';
import CustomerOrders from '../Orders/CustomerOrders.jsx';
import EmailConfirmationBanner from '../../components/account/EmailConfirmationBanner.jsx';
import { orderCheckoutUrl } from '../../utils/orders/orders.js';
import { api } from '../../services/api/client.js';
import { errorMessage } from '../../services/api/errors.js';
import { useCustomer } from '../../contexts/CustomerContext/CustomerContext.jsx';

const empty = { firstName: '', lastName: '', phone: '', email: '', address: '', password: '' };
const input = 'mt-2 w-full rounded-xl border border-stone-300 bg-white p-3';
const fields = [
  ['firstName', 'Nombre', 'text', 'given-name', 100],
  ['lastName', 'Apellido', 'text', 'family-name', 100],
  ['email', 'Correo electrónico', 'email', 'email', 254],
  ['phone', 'Teléfono (opcional)', 'tel', 'tel', 25],
];
export default function AccountView() {
  const { businessName } = useStore();
  const { session, updateSession, request } = useCustomer();
  const [mode, setMode] = useState('login');
  const [form, setForm] = useState(empty);
  const [editing, setEditing] = useState(false);
  const [savedProfile, setSavedProfile] = useState(empty);
  const [confirmation, setConfirmation] = useState('');
  const [loading, setLoading] = useState(!!session);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [registrationEmail, setRegistrationEmail] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [loaded, setLoaded] = useState(false);
  const [originalEmail, setOriginalEmail] = useState('');
  const [currentPassword, setCurrentPassword] = useState('');
  const query = new URLSearchParams(location.search);
  const showOrders = query.get('tab') === 'orders';
  const showAddresses = query.get('tab') === 'addresses';
  const expireSession = useCallback(() => {
    updateSession(null);
    setNotice('');
    setError('La sesión venció. Inicia sesión nuevamente para continuar en tu cuenta.');
  }, [updateSession]);
  useEffect(() => {
    if (!session) {
      setEditing(false);
      setSavedProfile(empty);
      setForm(empty);
      setLoaded(false);
      setLoading(false);
      return;
    }
    if (showOrders || showAddresses) {
      setLoading(false);
      return;
    }
    let active = true;
    setLoading(true);
    setLoaded(false);
    request('get')
      .then(({ data }) => {
        if (active) {
          setForm({ ...empty, ...data, phone: data.phone || '' });
          setSavedProfile({ ...empty, ...data, phone: data.phone || '' });
          setEditing(false);
          setOriginalEmail(data.email);
          setCurrentPassword('');
          setLoaded(true);
        }
      })
      .catch((e) => {
        if (active) setError(errorMessage(e));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [session, attempt, showOrders, showAddresses]);

  async function submit(event) {
    event.preventDefault();
    setError('');
    setNotice('');
    if (!session && mode === 'register' && form.password !== confirmation) {
      setError('Las contraseñas no coinciden.');
      return;
    }
    setBusy(true);
    try {
      const profile = {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        phone: form.phone.trim() || null,
      };
      if (session) {
        const { data } = await request('put', { ...profile, currentPassword });
        const changed = originalEmail.trim().toUpperCase() !== data.email.trim().toUpperCase();
        setForm({ ...empty, ...data, phone: data.phone || '' });
        setSavedProfile({ ...empty, ...data, phone: data.phone || '' });
        setEditing(false);
        setOriginalEmail(data.email);
        setCurrentPassword('');
        if (changed) {
          updateSession(null);
          setNotice('Correo actualizado. Inicia sesión con el nuevo correo y solicita su confirmación.');
        } else setNotice('Tu perfil se guardó correctamente.');
      } else {
        const payload =
          mode === 'register'
            ? { ...profile, password: form.password }
            : { email: form.email.trim(), password: form.password };
        const { data } = await api.post(`/customers/${mode}`, payload);
        if (mode === 'register') {
          setConfirmation('');
          setRegistrationEmail(data.email || profile.email);
          setForm(empty);
          setMode('login');
          setNotice(data.message || 'Cuenta creada. Revisa tu correo y confirma tu dirección antes de iniciar sesión.');
        } else {
          updateSession(data);
          setConfirmation('');
          setNotice('Bienvenido de nuevo.');
        }
      }
    } catch (e) {
      setError(e.response?.status === 401 && !session ? 'Correo o contraseña incorrectos.' : errorMessage(e));
    } finally {
      setBusy(false);
    }
  }
  const showProfile = !!session || mode === 'register';
  return (
    <>
      {session && query.get('returnTo') === 'checkout' && (
        <a
          href={orderCheckoutUrl(query.get('order'))}
          className="block bg-brand p-4 text-center font-semibold text-white"
        >
          Continuar con mi compra →
        </a>
      )}
      <header className="border-b border-brand/10 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-5 py-5">
          <a href="/" className="text-xl font-bold">
            {businessName}.
          </a>
          <a href="/" className="text-sm underline">
            Volver a la tienda
          </a>
        </div>
      </header>
      <main className="mx-auto max-w-2xl px-5 py-10">
        <p className="text-xs uppercase tracking-widest text-stone-500">Tu espacio</p>
        <h1 className="mt-3 text-3xl font-semibold">
          {session
            ? showOrders
              ? 'Mis pedidos'
              : showAddresses
                ? 'Mis direcciones'
                : 'Mi perfil'
            : mode === 'register'
              ? 'Crea tu cuenta'
              : 'Inicia sesión'}
        </h1>
        <p className="mt-3 text-sm text-stone-500">
          {session
            ? showOrders
              ? 'Consulta tus compras y sigue el avance de cada entrega.'
              : showAddresses
                ? 'Elige dónde recibir tus próximas compras.'
                : 'Mantén tus datos de contacto actualizados.'
            : 'Accede con tu correo y contraseña para guardar tus datos y continuar en tu cuenta.'}
        </p>
        {session && (
          <nav aria-label="Mi cuenta" className="mt-6 flex flex-wrap gap-3">
            <a
              href="/account"
              aria-current={!showOrders && !showAddresses ? 'page' : undefined}
              className={`rounded-xl px-4 py-3 text-sm ${!showOrders && !showAddresses ? 'bg-brand text-white' : 'border border-stone-300'}`}
            >
              Mi perfil
            </a>
            <a
              href="/account?tab=orders"
              aria-current={showOrders ? 'page' : undefined}
              className={`rounded-xl px-4 py-3 text-sm ${showOrders ? 'bg-brand text-white' : 'border border-stone-300'}`}
            >
              Mis pedidos
            </a>
            <a
              href="/account?tab=addresses"
              aria-current={showAddresses ? 'page' : undefined}
              className={`rounded-xl px-4 py-3 text-sm ${showAddresses ? 'bg-brand text-white' : 'border border-stone-300'}`}
            >
              Mis direcciones
            </a>
          </nav>
        )}
        {error && (
          <p role="alert" className="mt-5 rounded-xl bg-red-50 p-4 text-red-800">
            {error}
          </p>
        )}
        {notice && (
          <p role="status" className="mt-5 rounded-xl bg-emerald-50 p-4">
            {notice}
          </p>
        )}
        {registrationEmail && !session && (
          <a
            href={`/account/security?mode=confirm&email=${encodeURIComponent(registrationEmail)}`}
            className="mt-4 inline-block rounded-xl bg-brand px-5 py-3 font-semibold text-white"
          >
            Solicitar enlace de confirmación
          </a>
        )}
        {session && loaded && form.address && !showOrders && !showAddresses && (
          <p className="mt-4 rounded-xl bg-stone-50 p-4 text-sm">
            Dirección anterior (referencia): {form.address}. Completa sus campos en «Mis direcciones» para usarla en tu
            próxima compra.
          </p>
        )}
        {session && loaded && !showOrders && !showAddresses && (
          <EmailConfirmationBanner key={originalEmail} email={originalEmail} confirmed={form.emailConfirmed} />
        )}
        {!session && (
          <a href="/account/security?mode=reset" className="mt-5 inline-block text-sm underline">
            Olvidé mi contraseña
          </a>
        )}
        {session && showAddresses ? (
          <CustomerAddresses key={session.token} session={session} onExpired={expireSession} />
        ) : session && showOrders ? (
          <CustomerOrders key={session.token} session={session} onExpired={expireSession} />
        ) : loading ? (
          <p role="status" className="py-10">
            Cargando perfil…
          </p>
        ) : session && !loaded ? (
          <button
            onClick={() => {
              setError('');
              setAttempt((n) => n + 1);
            }}
            className="mt-5 underline"
          >
            Reintentar cargar perfil
          </button>
        ) : session && !editing ? (
          <section
            aria-label="Información de la cuenta"
            className="mt-7 rounded-2xl border border-stone-200 bg-white p-5 sm:p-7"
          >
            <h2 className="text-lg font-semibold">Información de tu cuenta</h2>
            <dl className="mt-5 grid gap-5 sm:grid-cols-2">
              {fields.map(([key, label]) => (
                <div key={key}>
                  <dt className="text-sm text-stone-500">{label.replace(' (opcional)', '')}</dt>
                  <dd className="mt-1 break-words font-medium">{savedProfile[key] || 'No registrado'}</dd>
                </div>
              ))}
            </dl>
            <div className="mt-6 flex flex-wrap gap-3">
              <button
                onClick={() => {
                  setError('');
                  setNotice('');
                  setEditing(true);
                }}
                className="rounded-xl bg-brand px-5 py-3 font-semibold text-white"
              >
                Editar perfil
              </button>
              <a
                href={
                  query.get('returnTo') === 'checkout'
                    ? '/account?tab=addresses&returnTo=checkout'
                    : '/account?tab=addresses'
                }
                className="rounded-xl border border-stone-300 px-5 py-3 font-semibold"
              >
                Administrar direcciones de envío
              </a>
            </div>
          </section>
        ) : (
          <form onSubmit={submit} className="mt-7 rounded-2xl border border-stone-200 bg-white p-5 sm:p-7">
            <fieldset disabled={busy} className="space-y-5">
              {session && <h2 className="text-lg font-semibold">Editar perfil</h2>}
              {session && originalEmail.trim().toUpperCase() !== form.email.trim().toUpperCase() && (
                <label className="block text-sm">
                  Contraseña actual para cambiar tu correo
                  <input
                    required
                    type="password"
                    maxLength={256}
                    autoComplete="current-password"
                    className={input}
                    value={currentPassword}
                    onChange={(e) => setCurrentPassword(e.target.value)}
                  />
                </label>
              )}
              <div className="grid gap-5 sm:grid-cols-2">
                {fields
                  .filter(([key]) => showProfile || key === 'email')
                  .map(([key, label, type, autoComplete, maxLength]) => (
                    <label key={key} className="text-sm font-medium">
                      {label}
                      <input
                        className={input}
                        type={type}
                        autoComplete={autoComplete}
                        required={key !== 'phone'}
                        maxLength={maxLength}
                        value={form[key]}
                        onChange={(e) => setForm({ ...form, [key]: e.target.value })}
                      />
                    </label>
                  ))}
              </div>
              {showProfile && (
                <p className="text-sm text-stone-500">
                  La dirección de entrega se completa al comprar o en «Mis direcciones».
                </p>
              )}
              {!session && (
                <label className="block text-sm font-medium">
                  Contraseña
                  <input
                    className={input}
                    type="password"
                    autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
                    required
                    minLength={mode === 'register' ? 12 : 1}
                    maxLength={256}
                    value={form.password}
                    onChange={(e) => setForm({ ...form, password: e.target.value })}
                  />
                  {mode === 'register' && (
                    <span className="text-xs font-normal text-stone-500">Al menos 12 caracteres.</span>
                  )}
                </label>
              )}
              {!session && mode === 'register' && (
                <label className="block text-sm font-medium">
                  Confirma tu contraseña
                  <input
                    className={input}
                    type="password"
                    autoComplete="new-password"
                    required
                    maxLength={256}
                    value={confirmation}
                    onChange={(e) => setConfirmation(e.target.value)}
                  />
                </label>
              )}
              <button className="w-full rounded-xl bg-brand p-4 font-semibold text-white disabled:opacity-40">
                {busy
                  ? 'Guardando…'
                  : session
                    ? 'Guardar cambios'
                    : mode === 'register'
                      ? 'Crear cuenta'
                      : 'Iniciar sesión'}
              </button>
              {session && (
                <button
                  type="button"
                  onClick={() => {
                    setForm({ ...savedProfile });
                    setCurrentPassword('');
                    setEditing(false);
                    setError('');
                    setNotice('');
                  }}
                  className="w-full rounded-xl border border-stone-300 p-3 font-semibold"
                >
                  Cancelar
                </button>
              )}
            </fieldset>
          </form>
        )}
        {session ? (
          <button
            disabled={busy}
            onClick={() => {
              updateSession(null);
              setNotice('Sesión cerrada.');
              setError('');
            }}
            className="mt-6 py-2 text-sm underline"
          >
            Cerrar sesión
          </button>
        ) : (
          <button
            disabled={busy}
            onClick={() => {
              setMode(mode === 'login' ? 'register' : 'login');
              setForm(empty);
              setConfirmation('');
              setError('');
              setNotice('');
            }}
            className="mt-6 py-2 text-sm underline"
          >
            {mode === 'login' ? '¿No tienes cuenta? Regístrate' : 'Ya tengo cuenta'}
          </button>
        )}
      </main>
    </>
  );
}
