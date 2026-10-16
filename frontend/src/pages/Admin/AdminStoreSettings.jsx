import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api/client.js';
import { getStoreSettings, saveStoreSettings } from '../../services/admin/adminService.js';
import { errorMessage } from '../../services/api/errors.js';
import { defaultStoreSettings } from '../../config/business.js';
import { useStore } from '../../contexts/StoreContext/StoreContext.jsx';
import StoreImage from '../../components/layout/StoreImage.jsx';
import { themeVariables } from '../../utils/storeTheme/storeTheme.js';

const input = 'mt-2 w-full rounded-xl border border-stone-300 bg-white p-3';
const button = 'rounded-xl bg-brand px-5 py-3 font-semibold text-white disabled:opacity-40';

export default function AdminStoreSettings({ session, onBusy }) {
  const { applySettings } = useStore();
  const lock = useRef(false);
  const [draft, setDraft] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setDraft(null);
    setError('');
    setNotice('');
    getStoreSettings(api, controller.signal)
      .then(({ data }) => {
        if (!controller.signal.aborted) setDraft({ ...defaultStoreSettings, ...data });
      })
      .catch((e) => {
        if (!controller.signal.aborted) setError(errorMessage(e));
      });
    return () => controller.abort();
  }, [revision]);
  async function save(event) {
    event.preventDefault();
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    onBusy(true);
    setError('');
    setNotice('');
    try {
      const { data } = await saveStoreSettings(session, draft);
      setDraft(data);
      applySettings(data);
      setNotice('Configuración guardada. La tienda mostrará estos cambios al abrirla o recargarla.');
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      lock.current = false;
      setBusy(false);
      onBusy(false);
    }
  }
  function change(key, value) {
    setDraft((current) => ({ ...current, [key]: value }));
  }
  return (
    <section className="rounded-2xl border border-stone-200 bg-white p-5 sm:p-7">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-semibold">Personaliza tu tienda</h2>
        <button
          disabled={busy}
          onClick={() => {
            if (!draft || window.confirm('Se descartarán los cambios sin guardar. ¿Recargar la configuración?'))
              setRevision((n) => n + 1);
          }}
          className="text-sm underline disabled:opacity-40"
        >
          Recargar configuración
        </button>
      </div>
      <p className="mt-3 text-sm text-stone-500">
        Identidad y contacto públicos de este negocio. Los cambios se publican al guardar.
      </p>
      {error && (
        <p role="alert" className="my-4 rounded-xl bg-red-50 p-4 text-red-800">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="my-4 rounded-xl bg-emerald-50 p-4">
          {notice}
        </p>
      )}
      {!draft && !error && (
        <p role="status" className="my-5">
          Cargando configuración…
        </p>
      )}
      {draft && (
        <form onSubmit={save} className="mt-6">
          <fieldset disabled={busy} className="space-y-5">
            <div className="grid gap-5 sm:grid-cols-2">
              <label className="text-sm">
                Nombre de la tienda
                <input
                  required
                  maxLength={100}
                  value={draft.name}
                  onChange={(e) => change('name', e.target.value)}
                  className={input}
                />
              </label>
              <label className="text-sm">
                WhatsApp del negocio (opcional)
                <input
                  type="tel"
                  maxLength={15}
                  pattern="[1-9][0-9]{7,14}"
                  placeholder="525512345678"
                  value={draft.whatsAppPhone}
                  onChange={(e) => change('whatsAppPhone', e.target.value)}
                  className={input}
                />
                <span className="mt-1 block text-xs text-stone-500">
                  Incluye el código de país, solo dígitos. Vacío deshabilita el contacto por WhatsApp.
                </span>
              </label>
            </div>
            <div className="grid gap-5 sm:grid-cols-2">
              {[
                ['logoUrl', 'URL del logo'],
                ['heroImageUrl', 'URL de imagen de portada'],
              ].map(([key, label]) => (
                <label key={key} className="text-sm">
                  {label} (opcional)
                  <input
                    type="url"
                    maxLength={2048}
                    placeholder="https://…"
                    value={draft[key]}
                    onChange={(e) => change(key, e.target.value.trim())}
                    className={input}
                  />
                </label>
              ))}
            </div>
            <p className="text-xs text-stone-500">
              Usa imágenes públicas con HTTPS. La carga de archivos con Cloudinary se incorporará después. Puedes dejar
              las imágenes vacías para mantener la presentación de texto.
            </p>
            <label className="block text-sm">
              Anuncio superior
              <input
                required
                maxLength={160}
                value={draft.announcement}
                onChange={(e) => change('announcement', e.target.value)}
                className={input}
              />
            </label>
            <label className="block text-sm">
              Título de portada
              <textarea
                required
                maxLength={100}
                rows={2}
                value={draft.heroTitle}
                onChange={(e) => change('heroTitle', e.target.value)}
                className={input}
              />
            </label>
            <label className="block text-sm">
              Descripción de portada
              <textarea
                required
                maxLength={500}
                rows={3}
                value={draft.heroDescription}
                onChange={(e) => change('heroDescription', e.target.value)}
                className={input}
              />
            </label>
            <label className="block text-sm">
              Descripción al pie de página
              <textarea
                required
                maxLength={500}
                rows={3}
                value={draft.footerDescription}
                onChange={(e) => change('footerDescription', e.target.value)}
                className={input}
              />
            </label>
            <h3 className="text-lg font-semibold">Colores e icono del navegador</h3>
            <p className="text-sm text-stone-500">
              Elige un color principal oscuro, fondo claro y acento claro para conservar la legibilidad.
            </p>
            <div className="grid gap-4 sm:grid-cols-3">
              {[
                ['primaryColor', 'Principal'],
                ['backgroundColor', 'Fondo'],
                ['accentColor', 'Acento'],
              ].map(([key, label]) => (
                <label key={key}>
                  {label}
                  <input
                    type="color"
                    value={draft[key]}
                    onChange={(e) => change(key, e.target.value)}
                    className="mt-2 block h-12 w-full"
                  />
                  <span className="text-xs">{draft[key]}</span>
                </label>
              ))}
            </div>
            <label className="block text-sm">
              URL del favicon (opcional)
              <input
                type="url"
                maxLength={2048}
                value={draft.faviconUrl}
                onChange={(e) => change('faviconUrl', e.target.value.trim())}
                className={input}
              />
              <span className="text-xs text-stone-500">
                Imagen cuadrada pública PNG, ICO o SVG. Vacío recupera el icono de ropa.
              </span>
            </label>
            <h3 className="text-lg font-semibold">Redes sociales y contacto</h3>
            <div className="grid gap-5 sm:grid-cols-2">
              {[
                ['instagramUrl', 'Instagram', 'url', 2048],
                ['facebookUrl', 'Facebook', 'url', 2048],
                ['tikTokUrl', 'TikTok', 'url', 2048],
                ['contactEmail', 'Correo de contacto', 'email', 254],
                ['contactPhone', 'Teléfono de contacto', 'tel', 30],
              ].map(([key, label, type, max]) => (
                <label key={key} className="text-sm">
                  {label} (opcional)
                  <input
                    type={type}
                    maxLength={max}
                    value={draft[key]}
                    onChange={(e) => change(key, e.target.value.trim())}
                    className={input}
                  />
                </label>
              ))}
            </div>
            {[
              ['businessAddress', 'Dirección del negocio', 500],
              ['businessHours', 'Horarios de atención', 500],
              ['shippingPolicy', 'Política de envíos', 6000],
              ['returnsPolicy', 'Política de cambios y devoluciones', 6000],
            ].map(([key, label, max]) => (
              <label key={key} className="block text-sm">
                {label} (opcional)
                <textarea
                  rows={key.endsWith('Policy') ? 7 : 3}
                  maxLength={max}
                  value={draft[key]}
                  onChange={(e) => change(key, e.target.value)}
                  className={input}
                />
              </label>
            ))}
            <p className="text-xs text-stone-500">
              Se publican como texto, sin HTML. Estas políticas informan al cliente; no cambian tarifas, inventario ni
              reglas de reembolso.
            </p>
            <button className={button}>{busy ? 'Guardando…' : 'Guardar configuración'}</button>
          </fieldset>
          <aside
            style={themeVariables(draft)}
            aria-label="Vista previa de la tienda"
            className="mt-8 overflow-hidden rounded-2xl border border-stone-200 bg-paper"
          >
            <p className="bg-stone-100 p-3 text-xs font-semibold">Vista previa · Sin publicar hasta guardar</p>
            <div className="flex items-center gap-3 p-4">
              <StoreImage src={draft.logoUrl} alt="Vista previa del logo" className="size-12 shrink-0 object-contain" />
              <p className="min-w-0 break-words font-bold">{draft.name}</p>
            </div>
            <p className="break-words bg-brand p-3 text-center text-xs text-white">{draft.announcement}</p>
            <div className="grid bg-[#182320] text-white sm:grid-cols-2">
              <div className="p-6">
                <h3 className="whitespace-pre-line break-words text-3xl font-bold">{draft.heroTitle}</h3>
                <p className="mt-4 whitespace-pre-line break-words text-sm text-white/75">{draft.heroDescription}</p>
              </div>
              <StoreImage
                src={draft.heroImageUrl}
                alt="Vista previa de portada"
                className="h-64 w-full object-cover"
                fallback={
                  <p className="flex items-center justify-center p-6 text-sm text-white/60">Portada sin imagen</p>
                }
              />
            </div>
          </aside>
        </form>
      )}
    </section>
  );
}
