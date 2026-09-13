import { businessName } from '../config/business.js';
import { useEffect, useState } from 'react';
import { api } from '../api/client.js';
import { errorMessage } from '../api/errors.js';
import { useCustomer } from '../context/CustomerContext.jsx';

const empty = { firstName: '', lastName: '', phone: '', email: '', address: '', password: '' };
const input = 'mt-2 w-full rounded-xl border border-stone-300 bg-white p-3';
const fields = [
  ['firstName', 'Nombre', 'text', 'given-name', 100],
  ['lastName', 'Apellido', 'text', 'family-name', 100],
  ['email', 'Correo electrónico', 'email', 'email', 254],
  ['phone', 'Teléfono (opcional)', 'tel', 'tel', 25],
];
export default function AccountView() {
  const { session, updateSession, request } = useCustomer();
  const [mode, setMode] = useState('login');
  const [form, setForm] = useState(empty);
  const [confirmation, setConfirmation] = useState('');
  const [loading, setLoading] = useState(!!session);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [loaded, setLoaded] = useState(false);
  useEffect(() => {
    if (!session) { setForm(empty); setLoaded(false); setLoading(false); return; }
    let active = true;
    setLoading(true); setLoaded(false);
    request('get').then(({ data }) => { if (active) { setForm({ ...empty, ...data, phone: data.phone || '' }); setLoaded(true); } })
      .catch(e => { if (active) setError(errorMessage(e)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [session, attempt]);

  async function submit(event) {
    event.preventDefault(); setError(''); setNotice('');
    if (!session && mode === 'register' && form.password !== confirmation) { setError('Las contraseñas no coinciden.'); return; }
    setBusy(true);
    try {
      const profile = { firstName: form.firstName.trim(), lastName: form.lastName.trim(), email: form.email.trim(), phone: form.phone.trim() || null, address: form.address.trim() };
      if (session) {
        const { data } = await request('put', profile);
        setForm({ ...empty, ...data, phone: data.phone || '' }); setNotice('Tu perfil se guardó correctamente.');
      } else {
        const payload = mode === 'register' ? { ...profile, password: form.password } : { email: form.email.trim(), password: form.password };
        const { data } = await api.post(`/customers/${mode}`, payload);
        updateSession(data); setConfirmation(''); setNotice(mode === 'register' ? 'Tu cuenta está lista.' : 'Bienvenido de nuevo.');
      }
    } catch (e) { setError(e.response?.status === 401 && !session ? 'Correo o contraseña incorrectos.' : errorMessage(e)); }
    finally { setBusy(false); }
  }
  const showProfile = !!session || mode === 'register';
  return <>
    <header className="border-b border-brand/10 bg-white"><div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-5 py-5"><a href="/" className="text-xl font-bold">{businessName}.</a><a href="/" className="text-sm underline">Volver a la tienda</a></div></header>
    <main className="mx-auto max-w-2xl px-5 py-10">
      <p className="text-xs uppercase tracking-widest text-stone-500">Tu espacio</p><h1 className="mt-3 text-3xl font-semibold">{session ? 'Mi perfil' : mode === 'register' ? 'Crea tu cuenta' : 'Inicia sesión'}</h1>
      <p className="mt-3 text-sm text-stone-500">{session ? 'Mantén tus datos y tu dirección de entrega actualizados.' : 'Accede con tu correo y contraseña para guardar tus datos de entrega.'}</p>
      {error && <p role="alert" className="mt-5 rounded-xl bg-red-50 p-4 text-red-800">{error}</p>}
      {notice && <p role="status" className="mt-5 rounded-xl bg-emerald-50 p-4">{notice}</p>}
      {loading ? <p role="status" className="py-10">Cargando perfil…</p> : session && !loaded ? <button onClick={() => { setError(''); setAttempt(n => n + 1); }} className="mt-5 underline">Reintentar cargar perfil</button> : <form onSubmit={submit} className="mt-7 rounded-2xl border border-stone-200 bg-white p-5 sm:p-7">
        <fieldset disabled={busy} className="space-y-5">
          <div className="grid gap-5 sm:grid-cols-2">{fields.filter(([key]) => showProfile || key === 'email').map(([key, label, type, autoComplete, maxLength]) => <label key={key} className="text-sm font-medium">{label}<input className={input} type={type} autoComplete={autoComplete} required={key !== 'phone'} maxLength={maxLength} value={form[key]} onChange={e => setForm({ ...form, [key]: e.target.value })} /></label>)}</div>
          {showProfile && <label className="block text-sm font-medium">Dirección de entrega<textarea className={input} autoComplete="street-address" required minLength={10} maxLength={500} rows={4} placeholder="Calle y número, colonia, ciudad, estado, código postal y país" value={form.address} onChange={e => setForm({ ...form, address: e.target.value })} /><span className="mt-1 block text-xs font-normal text-stone-500">Incluye número interior y referencias si son necesarias.</span></label>}
          {!session && <label className="block text-sm font-medium">Contraseña<input className={input} type="password" autoComplete={mode === 'register' ? 'new-password' : 'current-password'} required minLength={mode === 'register' ? 12 : 1} maxLength={256} value={form.password} onChange={e => setForm({ ...form, password: e.target.value })} />{mode === 'register' && <span className="text-xs font-normal text-stone-500">Al menos 12 caracteres.</span>}</label>}
          {!session && mode === 'register' && <label className="block text-sm font-medium">Confirma tu contraseña<input className={input} type="password" autoComplete="new-password" required maxLength={256} value={confirmation} onChange={e => setConfirmation(e.target.value)} /></label>}
          <button className="w-full rounded-xl bg-brand p-4 font-semibold text-white disabled:opacity-40">{busy ? 'Guardando…' : session ? 'Guardar cambios' : mode === 'register' ? 'Crear cuenta' : 'Iniciar sesión'}</button>
        </fieldset>
      </form>}
      {session ? <button disabled={busy} onClick={() => { updateSession(null); setNotice('Sesión cerrada.'); setError(''); }} className="mt-6 py-2 text-sm underline">Cerrar sesión</button> : <button disabled={busy} onClick={() => { setMode(mode === 'login' ? 'register' : 'login'); setForm(empty); setConfirmation(''); setError(''); setNotice(''); }} className="mt-6 py-2 text-sm underline">{mode === 'login' ? '¿No tienes cuenta? Regístrate' : 'Ya tengo cuenta'}</button>}
    </main>
  </>;
}

