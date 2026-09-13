import { businessName } from '../config/business.js';
import { useEffect, useState } from 'react';
import { api } from '../api/client.js';
import { adminRequest, readSession, saveSession } from '../api/admin.js';
import { errorMessage } from '../api/errors.js';
import { formatMXN } from '../utils/money.js';

const emptyProduct = { name: '', description: '', price: '', imageUrl: '', categoryId: '', isActive: true };
const emptyCategory = { name: '', description: '' };
const inputClass = 'mt-1 w-full rounded-lg border border-stone-300 bg-white p-3';
const buttonClass = 'rounded-lg bg-brand px-4 py-3 font-semibold text-white disabled:opacity-40';

function Field({ label, children }) { return <label className="block text-sm font-medium">{label}{children}</label>; }

function AdminProductImage({ imageUrl, name }) {
  const [failed, setFailed] = useState(false);
  return <div className="flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-stone-200 bg-white">
    {imageUrl && !failed ? <img src={imageUrl} alt={name} loading="lazy" onError={() => setFailed(true)} className="h-full w-full object-contain p-1" /> : <span className="p-1 text-center text-[10px] text-stone-500">Sin imagen</span>}
  </div>;
}

export default function AdminView() {
  const [session, setSession] = useState(readSession);
  const [credentials, setCredentials] = useState({ username: '', password: '' });
  const [products, setProducts] = useState([]);
  const [categories, setCategories] = useState([]);
  const [tab, setTab] = useState('products');
  const [draft, setDraft] = useState(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [reload, setReload] = useState(0);

  function logout() { saveSession(null); setSession(null); setProducts([]); setCategories([]); setDraft(null); }
  function handleError(e) { setError(errorMessage(e)); if (e.response?.status === 401) logout(); }

  useEffect(() => {
    if (!session) return;
    const timer = setTimeout(() => { logout(); setError('La sesión venció. Inicia sesión nuevamente.'); }, Math.max(0, Date.parse(session.expiresAtUtc) - Date.now()));
    return () => clearTimeout(timer);
  }, [session]);

  useEffect(() => {
    if (!session) return;
    let active = true;
    setLoading(true);
    Promise.all([adminRequest(session, 'get', '/products'), adminRequest(session, 'get', '/categories')])
      .then(([p, c]) => { if (active) { setProducts(p.data); setCategories(c.data); } })
      .catch(e => { if (active) handleError(e); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [session, reload]);

  async function login(event) {
    event.preventDefault(); setBusy(true); setError('');
    try {
      const { data } = await api.post('/auth/login', credentials);
      saveSession(data); setSession(data); setCredentials({ username: '', password: '' });
    } catch (e) { setError(e.response?.status === 401 ? 'Usuario o contraseña incorrectos.' : errorMessage(e)); }
    finally { setBusy(false); }
  }

  async function save(event) {
    event.preventDefault(); setBusy(true); setError(''); setNotice('');
    try {
      const payload = tab === 'products' ? { ...draft, price: Number(draft.price), categoryId: Number(draft.categoryId) } : draft;
      await adminRequest(session, draft.id ? 'put' : 'post', `/${tab}${draft.id ? `/${draft.id}` : ''}`, payload);
      setDraft(null); setReload(n => n + 1); setNotice('Cambios guardados.');
    } catch (e) { handleError(e); }
    finally { setBusy(false); }
  }

  async function remove(item) {
    if (!window.confirm(`¿Eliminar “${item.name}”? Esta acción no se puede deshacer.`)) return;
    setBusy(true); setError(''); setNotice('');
    try { await adminRequest(session, 'delete', `/${tab}/${item.id}`); setReload(n => n + 1); setNotice('Elemento eliminado.'); }
    catch (e) { handleError(e); }
    finally { setBusy(false); }
  }

  return <main className="mx-auto max-w-5xl px-5 py-8">
    <header className="mb-8 flex flex-wrap items-center justify-between gap-4"><div><p className="text-sm text-stone-500">Administración</p><h1 className="text-3xl font-semibold">{businessName}</h1></div><div className="flex items-center gap-5"><a href="/" className="underline">Ver tienda</a>{session && <button disabled={busy} onClick={logout} className="underline">Cerrar sesión</button>}</div></header>
    {error && <div role="alert" className="mb-5 rounded-xl bg-red-50 p-4 text-red-800">{error}{session && <button className="ml-3 underline" onClick={() => { setError(''); setReload(n => n + 1); }}>Reintentar carga</button>}</div>}
    {notice && session && <p role="status" className="mb-5 rounded-xl bg-emerald-50 p-4">{notice}</p>}
    {!session ? <form onSubmit={login} className="mx-auto max-w-md space-y-5 rounded-2xl border border-stone-200 bg-white p-6"><h2 className="text-xl font-semibold">Accede a tu negocio</h2><Field label="Usuario"><input className={inputClass} autoComplete="username" required maxLength={100} value={credentials.username} onChange={e => setCredentials({ ...credentials, username: e.target.value })} /></Field><Field label="Contraseña"><input className={inputClass} type="password" autoComplete="current-password" required maxLength={256} value={credentials.password} onChange={e => setCredentials({ ...credentials, password: e.target.value })} /></Field><button disabled={busy} className={`${buttonClass} w-full`}>{busy ? 'Ingresando…' : 'Iniciar sesión'}</button></form> : <>
      <nav aria-label="Administración" className="mb-6 flex gap-3">{[['products', 'Productos'], ['categories', 'Categorías']].map(([value, label]) => <button key={value} disabled={busy} aria-pressed={tab === value} className={tab === value ? buttonClass : 'rounded-lg border border-stone-300 px-4 py-3'} onClick={() => { setTab(value); setDraft(null); setNotice(''); }}>{label}</button>)}</nav>
      {draft ? <form onSubmit={save} className="space-y-5 rounded-2xl border border-stone-200 bg-white p-6"><h2 className="text-xl font-semibold">{draft.id ? 'Editar' : 'Crear'} {tab === 'products' ? 'producto' : 'categoría'}</h2><fieldset disabled={busy} className="space-y-5"><Field label="Nombre"><input className={inputClass} required maxLength={tab === 'products' ? 160 : 100} value={draft.name} onChange={e => setDraft({ ...draft, name: e.target.value })} /></Field><Field label="Descripción"><textarea className={inputClass} rows={3} maxLength={2000} value={draft.description} onChange={e => setDraft({ ...draft, description: e.target.value })} /></Field>
      {tab === 'products' && <><div className="grid gap-5 sm:grid-cols-2"><Field label="Precio (MXN)"><input className={inputClass} type="number" min="0.01" max="9999999999.99" step="0.01" required value={draft.price} onChange={e => setDraft({ ...draft, price: e.target.value })} /></Field><Field label="Categoría"><select className={inputClass} required value={draft.categoryId} onChange={e => setDraft({ ...draft, categoryId: e.target.value })}><option value="">Selecciona una categoría</option>{categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></Field></div><Field label="URL de imagen (HTTP o HTTPS)"><input className={inputClass} type="url" pattern="https?://.*" maxLength={2048} required value={draft.imageUrl} onChange={e => setDraft({ ...draft, imageUrl: e.target.value })} /></Field><label className="flex items-center gap-3"><input type="checkbox" checked={draft.isActive} onChange={e => setDraft({ ...draft, isActive: e.target.checked })} />Visible en el catálogo</label></>}
      <div className="flex gap-4"><button className={buttonClass}>{busy ? 'Guardando…' : 'Guardar'}</button><button type="button" onClick={() => setDraft(null)} className="px-4 py-3 underline">Cancelar</button></div></fieldset></form> : <>
      <div className="mb-5 flex flex-wrap items-center justify-between gap-4"><h2 className="text-xl font-semibold">{tab === 'products' ? 'Tus productos' : 'Tus categorías'}</h2><button className={buttonClass} disabled={busy || loading || (tab === 'products' && !categories.length)} onClick={() => setDraft({ ...(tab === 'products' ? emptyProduct : emptyCategory) })}>Crear {tab === 'products' ? 'producto' : 'categoría'}</button></div>
      {loading ? <p role="status">Cargando…</p> : <ul className="divide-y divide-stone-200 rounded-xl border border-stone-200 bg-white">{(tab === 'products' ? products : categories).map(item => <li key={item.id} className="flex flex-wrap items-center justify-between gap-4 p-5"><div className="flex min-w-0 items-center gap-4">{tab === 'products' && <AdminProductImage key={item.imageUrl} imageUrl={item.imageUrl} name={item.name} />}<div className="min-w-0"><h3 className="break-words font-semibold">{item.name}</h3><p className="text-sm text-stone-500">{tab === 'products' ? `${formatMXN(item.price)} · ${item.isActive ? 'Activo' : 'Inactivo'} · ${item.categoryName} · ${item.viewCount ?? 0} visitas` : item.description}</p></div></div><div className="flex gap-4"><button disabled={busy} onClick={() => setDraft({ ...item })} className="p-2 underline">Editar</button><button disabled={busy} onClick={() => remove(item)} className="p-2 text-red-700 underline">Eliminar</button></div></li>)}</ul>}
      {!loading && !(tab === 'products' ? products : categories).length && <p className="py-8 text-stone-500">No hay elementos. Crea {tab === 'products' && !categories.length ? 'primero una categoría' : 'el primero para comenzar'}.</p>}
      </>}
    </>}
  </main>;
}


