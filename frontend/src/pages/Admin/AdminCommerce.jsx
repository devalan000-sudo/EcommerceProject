import ProductImageUpload from './ProductImageUpload.jsx';
import { sizeLabel, templateSizes, popularColors } from '../../utils/variants/variantOptions.js';
import { useEffect, useState } from 'react';
import { errorMessage } from '../../services/api/errors.js';
import {
  createVariantTemplate,
  getProductVariants,
  getShippingOptions,
  saveProductVariant,
  saveShippingOption,
} from '../../services/admin/adminService.js';
import { formatMXN } from '../../utils/money/money.js';

const input = 'mt-1 w-full rounded-lg border border-stone-300 bg-white p-3';
const button = 'rounded-lg bg-brand px-4 py-3 text-sm font-semibold text-white disabled:opacity-40';
const emptyVariant = { size: 'S', color: '', imageUrl: '', stock: 0, isActive: true };
const emptyShipping = { name: '', description: '', kind: 'FlatRate', price: 0, isActive: false };

function VariantEditor({ session, products, onSaved }) {
  const [template, setTemplate] = useState({ mainColor: 'Original', initialStock: 0 });
  const [notice, setNotice] = useState('');
  const [productId, setProductId] = useState('');
  const [variants, setVariants] = useState([]);
  const [draft, setDraft] = useState(emptyVariant);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let current = true;
    setDraft(emptyVariant);
    setVariants([]);
    setError('');
    setLoading(false);
    if (!productId) return;
    setLoading(true);
    getProductVariants(session, productId)
      .then((r) => {
        if (current) setVariants(r.data);
      })
      .catch((e) => {
        if (current) setError(errorMessage(e));
      })
      .finally(() => {
        if (current) setLoading(false);
      });
    return () => {
      current = false;
    };
  }, [productId, session, revision]);
  async function save(event) {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      await saveProductVariant(session, productId, { ...draft, stock: Number(draft.stock) });
      setRevision((n) => n + 1);
      onSaved();
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  }
  async function applyTemplate() {
    if (busy) return;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const { data } = await createVariantTemplate(session, productId, {
        ...template,
        initialStock: Number(template.initialStock),
      });
      setRevision((n) => n + 1);
      onSaved();
      setNotice(data.added + ' combinaciones agregadas. Las variantes existentes se conservaron.');
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="rounded-xl border border-stone-200 bg-white p-5">
      <h3 className="text-lg font-semibold">Inventario por talla y color</h3>
      <p className="mt-2 text-sm text-stone-500">
        Registra existencias físicas. Las reservas activas se descuentan de la disponibilidad, no de este número.
      </p>
      <label className="mt-4 block text-sm">
        Producto
        <select disabled={busy} className={input} value={productId} onChange={(e) => setProductId(e.target.value)}>
          <option value="">Selecciona un producto</option>
          {products.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
      </label>
      {error && (
        <p role="alert" className="my-3 text-sm text-red-700">
          {error}
        </p>
      )}
      {loading && (
        <p role="status" className="my-3">
          Cargando variantes…
        </p>
      )}
      {notice && (
        <p role="status" className="my-3 text-sm text-emerald-800">
          {notice}
        </p>
      )}
      {productId && !loading && (
        <>
          <fieldset disabled={busy} className="my-5 space-y-3 rounded-xl bg-stone-50 p-4">
            <legend className="font-semibold">Plantilla de tallas y colores</legend>
            <p className="text-sm text-stone-500">
              Agrega Chica, Mediana y Grande en el color de la fotografía y otros tres colores populares. No altera
              stock ni variantes existentes. Para calzado o accesorios puedes conservar tallas numéricas o únicas.
            </p>
            <label className="block text-sm">
              Color de la imagen principal
              <input
                maxLength={50}
                value={template.mainColor}
                onChange={(e) => setTemplate({ ...template, mainColor: e.target.value })}
                className={input}
              />
            </label>
            <label className="block text-sm">
              Stock inicial por combinación nueva
              <input
                type="number"
                min="0"
                max="1000000"
                step="1"
                value={template.initialStock}
                onChange={(e) => setTemplate({ ...template, initialStock: e.target.value })}
                className={input}
              />
            </label>
            <button
              type="button"
              disabled={
                !template.mainColor.trim() ||
                !Number.isInteger(Number(template.initialStock)) ||
                Number(template.initialStock) < 0 ||
                Number(template.initialStock) > 1000000
              }
              onClick={applyTemplate}
              className={button}
            >
              Completar tallas y colores
            </button>
          </fieldset>
          <ul className="my-4 divide-y divide-stone-200">
            {variants.map((v) => (
              <li key={v.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
                <div>
                  <p className="font-semibold">
                    {sizeLabel(v.size)} · {v.color}
                  </p>
                  <p className="text-xs text-stone-500">
                    Existencias: {v.stock} · Disponibles: {v.availableStock} · {v.isActive ? 'Activa' : 'Inactiva'}
                  </p>
                </div>
                <button disabled={busy} onClick={() => setDraft(v)} className="p-2 text-sm underline">
                  Editar variante
                </button>
              </li>
            ))}
          </ul>
          <datalist id="variant-sizes">
            {templateSizes.map((s) => (
              <option key={s} value={s}>
                {sizeLabel(s)}
              </option>
            ))}
          </datalist>
          <datalist id="variant-colors">
            {popularColors.map((c) => (
              <option key={c} value={c} />
            ))}
          </datalist>
          <form onSubmit={save}>
            <fieldset disabled={busy} className="space-y-3">
              <div className="grid gap-3 sm:grid-cols-3">
                <label className="text-sm">
                  Talla
                  <input
                    list="variant-sizes"
                    required
                    maxLength={30}
                    value={draft.size}
                    onChange={(e) => setDraft({ ...draft, size: e.target.value })}
                    className={input}
                    placeholder="M / Única / 27"
                  />
                </label>
                <label className="text-sm">
                  Color
                  <input
                    list="variant-colors"
                    required
                    maxLength={50}
                    value={draft.color}
                    onChange={(e) => setDraft({ ...draft, color: e.target.value })}
                    className={input}
                    placeholder="Negro"
                  />
                </label>
                <label className="text-sm">
                  Existencias
                  <input
                    type="number"
                    required
                    min="0"
                    max="1000000"
                    step="1"
                    value={draft.stock}
                    onChange={(e) => setDraft({ ...draft, stock: e.target.value })}
                    className={input}
                  />
                </label>
              </div>
              <ProductImageUpload
                session={session}
                imageUrl={draft.imageUrl || ''}
                disabled={busy}
                onBusy={setBusy}
                label="Subir fotografía de este color"
                onUploaded={(imageUrl) => setDraft((current) => ({ ...current, imageUrl }))}
              />
              <label className="block text-sm">
                URL de la fotografía de este color (opcional)
                <input
                  type="url"
                  maxLength={2048}
                  value={draft.imageUrl || ''}
                  onChange={(e) => setDraft({ ...draft, imageUrl: e.target.value })}
                  className={input}
                />
              </label>
              <p className="text-xs text-stone-500">
                Al guardar, la fotografía se aplica a todas las tallas de este color. Sin fotografía, el cliente verá la
                imagen general como referencia.
              </p>
              <label className="flex gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={draft.isActive}
                  onChange={(e) => setDraft({ ...draft, isActive: e.target.checked })}
                />
                Variante activa
              </label>
              <button className={button}>
                {busy ? 'Guardando…' : draft.id ? 'Guardar variante' : 'Agregar variante'}
              </button>
              {draft.id && (
                <button type="button" className="ml-4 text-sm underline" onClick={() => setDraft(emptyVariant)}>
                  Cancelar edición
                </button>
              )}
            </fieldset>
          </form>
        </>
      )}
    </section>
  );
}

function ShippingEditor({ session }) {
  const [options, setOptions] = useState([]);
  const [draft, setDraft] = useState(emptyShipping);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let current = true;
    getShippingOptions(session)
      .then((r) => {
        if (current) setOptions(r.data);
      })
      .catch((e) => {
        if (current) setError(errorMessage(e));
      });
    return () => {
      current = false;
    };
  }, [session, revision]);
  async function save(event) {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      await saveShippingOption(session, { ...draft, price: draft.kind === 'FlatRate' ? Number(draft.price) : 0 });
      setDraft(emptyShipping);
      setRevision((n) => n + 1);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="mt-5 rounded-xl border border-stone-200 bg-white p-5">
      <h3 className="text-lg font-semibold">Opciones de entrega</h3>
      <p className="mt-2 text-sm text-stone-500">
        Configura tarifa fija, envío gratuito o recogida. Describe la cobertura o ubicación. El checkout actual no
        muestra este selector ni añade cargos de envío. Estas opciones se conservan para configuraciones futuras.
      </p>
      {error && (
        <p role="alert" className="my-3 text-sm text-red-700">
          {error}
        </p>
      )}
      <ul className="my-4 divide-y divide-stone-200">
        {options.map((s) => (
          <li key={s.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
            <div>
              <p className="font-semibold">
                {s.name} · {formatMXN(s.price)}
              </p>
              <p className="text-xs text-stone-500">
                {s.description} · {s.isActive ? 'Activa' : 'Inactiva'}
              </p>
            </div>
            <button disabled={busy} onClick={() => setDraft(s)} className="p-2 text-sm underline">
              Editar entrega
            </button>
          </li>
        ))}
      </ul>
      <form onSubmit={save}>
        <fieldset disabled={busy} className="space-y-3">
          <label className="block text-sm">
            Nombre
            <input
              required
              maxLength={100}
              value={draft.name}
              onChange={(e) => setDraft({ ...draft, name: e.target.value })}
              className={input}
            />
          </label>
          <label className="block text-sm">
            Descripción / condiciones
            <textarea
              maxLength={500}
              value={draft.description}
              onChange={(e) => setDraft({ ...draft, description: e.target.value })}
              className={input}
            />
          </label>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="text-sm">
              Tipo
              <select
                value={draft.kind}
                onChange={(e) => setDraft({ ...draft, kind: e.target.value, price: 0 })}
                className={input}
              >
                <option value="FlatRate">Tarifa fija</option>
                <option value="Free">Envío gratuito</option>
                <option value="Pickup">Recogida en tienda</option>
              </select>
            </label>
            <label className="text-sm">
              Precio MXN
              <input
                type="number"
                disabled={draft.kind !== 'FlatRate'}
                required
                min="0"
                max="999999.99"
                step="0.01"
                value={draft.price}
                onChange={(e) => setDraft({ ...draft, price: e.target.value })}
                className={input}
              />
            </label>
          </div>
          <label className="flex gap-2 text-sm">
            <input
              type="checkbox"
              checked={draft.isActive}
              onChange={(e) => setDraft({ ...draft, isActive: e.target.checked })}
            />
            Opción activa
          </label>
          <button className={button}>
            {busy ? 'Guardando…' : draft.id ? 'Guardar entrega' : 'Agregar opción de entrega'}
          </button>
          {draft.id && (
            <button type="button" onClick={() => setDraft(emptyShipping)} className="ml-4 text-sm underline">
              Cancelar edición
            </button>
          )}
        </fieldset>
      </form>
    </section>
  );
}

export default function AdminCommerce({ session, products, onSaved, initiallyOpen = false }) {
  const [open, setOpen] = useState(initiallyOpen);
  return (
    <details
      open={open}
      onToggle={(e) => setOpen(e.currentTarget.open)}
      className="mb-7 rounded-xl border border-brand/20 p-4"
    >
      <summary className="cursor-pointer font-semibold">Inventario y opciones de entrega</summary>
      <div className="mt-4">
        <VariantEditor session={session} products={products} onSaved={onSaved} />
        <ShippingEditor session={session} />
      </div>
    </details>
  );
}
