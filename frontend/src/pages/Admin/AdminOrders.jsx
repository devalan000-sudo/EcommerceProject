import { useEffect, useRef, useState } from 'react';
import { errorMessage } from '../../services/api/errors.js';
import { getAdminOrder, getAdminOrders, updateFulfillment } from '../../services/admin/adminService.js';
import { formatMXN } from '../../utils/money/money.js';
import ManualPaymentForm from './ManualPaymentForm.jsx';
import OrderResolutionPanel from './OrderResolutionPanel.jsx';

const states = {
  PendingConfirmation: 'Pendiente',
  Preparing: 'Preparando',
  Shipped: 'Enviado',
  Delivered: 'Entregado',
  Cancelled: 'Cancelado',
};
const payments = { Pending: 'Pendiente', Paid: 'Pagado', Failed: 'Fallido / vencido', Refunded: 'Reembolsado' };
const methods = { Card: 'Tarjeta · Stripe', WhatsApp: 'WhatsApp' };
const input = 'mt-1 w-full rounded-lg border border-stone-300 bg-white p-3';
const button = 'rounded-lg bg-brand px-4 py-3 text-sm font-semibold text-white disabled:opacity-40';
const empty = { search: '', paymentMethod: '', paymentStatus: '', fulfillmentStatus: '', from: '', to: '' };
const date = (value) =>
  new Intl.DateTimeFormat('es-MX', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }).format(
    new Date(value.endsWith('Z') ? value : `${value}Z`),
  );
const draftOf = (o) => ({
  status: o.fulfillmentStatus,
  carrier: o.carrier || '',
  trackingNumber: o.trackingNumber || '',
  version: o.fulfillmentVersion,
});

function Detail({ session, id, onClose, onSaved }) {
  const [order, setOrder] = useState(null);
  const [draft, setDraft] = useState(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const lock = useRef(false);
  useEffect(() => {
    let current = true;
    setOrder(null);
    setError('');
    getAdminOrder(session, id)
      .then(({ data }) => {
        if (current) {
          setOrder(data);
          setDraft(draftOf(data));
        }
      })
      .catch((e) => {
        if (current) setError(errorMessage(e));
      });
    return () => {
      current = false;
    };
  }, [id, session, revision]);
  async function save(event) {
    event.preventDefault();
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const { data } = await updateFulfillment(session, id, draft);
      setOrder(data);
      setDraft(draftOf(data));
      setNotice('Entrega actualizada.');
      onSaved();
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  const closed = order && ['Delivered', 'Cancelled'].includes(order.fulfillmentStatus);
  const unpaid = order?.paymentMethod === 'Card' && order.paymentStatus !== 'Paid';
  const resolving = order?.cancellationPending || !!order?.refundStatus;
  const next = { PendingConfirmation: ['Preparing'], Preparing: ['Shipped', 'Delivered'], Shipped: ['Delivered'] };
  return (
    <section aria-label="Detalle del pedido" className="rounded-2xl border border-stone-200 bg-white p-5 sm:p-7">
      <div className="mb-5 flex flex-wrap justify-between gap-3">
        <h2 className="text-xl font-semibold">Detalle del pedido</h2>
        <div className="flex gap-4">
          <button
            disabled={busy}
            onClick={() => {
              setNotice('');
              setRevision((n) => n + 1);
            }}
            className="underline"
          >
            Actualizar detalle
          </button>
          <button disabled={busy} onClick={onClose} className="underline">
            Volver al listado
          </button>
        </div>
      </div>
      {error && (
        <p role="alert" className="mb-4 rounded-lg bg-red-50 p-3 text-red-800">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="mb-4 rounded-lg bg-emerald-50 p-3">
          {notice}
        </p>
      )}
      {!order && !error && <p role="status">Cargando pedido…</p>}
      {order && (
        <>
          <p className="break-all font-semibold">{order.number}</p>
          <p className="mt-1 text-sm text-stone-500">{date(order.createdAtUtc)} UTC</p>
          <div className="my-5 grid gap-5 sm:grid-cols-2">
            <div>
              <h3 className="font-semibold">Cliente y entrega</h3>
              <p>
                {order.firstName} {order.lastName}
              </p>
              <p className="break-all">{order.email}</p>
              <p>{order.phone || 'Sin teléfono'}</p>
              <p className="mt-2 whitespace-pre-wrap">{order.address}</p>
              <p className="mt-2 text-sm">{order.shippingName || 'Entrega por acordar'}</p>
            </div>
            <div>
              <h3 className="font-semibold">Pago y seguimiento</h3>
              <p>
                {methods[order.paymentMethod]} · {payments[order.paymentStatus]}
              </p>
              <p>{states[order.fulfillmentStatus]}</p>
              <p className="mt-2 break-words text-sm">
                {order.carrier ? `${order.carrier} · Guía: ${order.trackingNumber}` : 'Sin guía registrada'}
              </p>
            </div>
          </div>
          <ul className="divide-y divide-stone-200">
            {order.items.map((item, index) => (
              <li key={index} className="flex flex-wrap gap-3 py-4">
                <img src={item.imageUrl} alt="" className="size-16 shrink-0 rounded-lg bg-stone-50 object-contain" />
                <div className="min-w-0 flex-1">
                  <p className="font-semibold">
                    {item.quantity} × {item.productName}
                  </p>
                  <p className="text-sm text-stone-500">
                    {[item.size, item.color, item.details].filter(Boolean).join(' · ')}
                  </p>
                  <p className="text-sm">{formatMXN(item.unitPrice)} c/u</p>
                </div>
                <p>{formatMXN(item.lineTotal)}</p>
              </li>
            ))}
          </ul>
          <div className="my-5 border-t border-stone-200 pt-4 text-right">
            <p>Productos: {formatMXN(order.subtotal)}</p>
            <p>Entrega: {order.shippingCost == null ? 'Por acordar' : formatMXN(order.shippingCost)}</p>
            <p className="mt-2 text-lg font-semibold">
              Total: {order.total == null ? 'Pendiente de cotizar' : `${formatMXN(order.total)} MXN`}
            </p>
          </div>
          {order.paymentMethod === 'WhatsApp' &&
            order.paymentStatus === 'Pending' &&
            !resolving &&
            order.fulfillmentStatus !== 'Cancelled' && (
              <ManualPaymentForm
                key={`${order.id}:${order.fulfillmentVersion}`}
                session={session}
                order={order}
                disabled={busy}
                onBusy={setBusy}
                onConfirmed={(data) => {
                  setOrder(data);
                  setDraft(draftOf(data));
                  setNotice('Pago registrado e inventario descontado.');
                  onSaved();
                }}
              />
            )}
          {order.manualPaymentConfirmedAtUtc && (
            <p className="my-4 rounded-lg bg-emerald-50 p-3 text-sm">
              Pago manual registrado: {date(order.manualPaymentConfirmedAtUtc)} UTC ·{' '}
              {{ Cash: 'Efectivo', Transfer: 'Transferencia', Other: 'Otro' }[order.manualPaymentMethod]}. Inventario
              descontado.
            </p>
          )}
          {order.paymentMethod === 'WhatsApp' && order.paymentStatus === 'Pending' && (
            <p className="mb-4 text-sm text-stone-500">
              Cambiar únicamente la entrega no confirma el pago ni descuenta inventario. Utiliza la confirmación de pago
              para registrar la venta.
            </p>
          )}
          {unpaid && (
            <p className="rounded-lg bg-amber-50 p-3 text-sm">
              La entrega se habilita cuando Stripe confirma el pago. El estado de pago no se modifica desde este
              formulario.
            </p>
          )}
          {closed && (
            <p className="text-sm text-stone-500">Pedido cerrado. El seguimiento se conserva para consulta.</p>
          )}
          <OrderResolutionPanel
            key={`resolution:${order.id}`}
            order={order}
            session={session}
            busy={busy}
            onBusy={setBusy}
            onUpdated={(data) => {
              setOrder(data);
              setDraft(draftOf(data));
              setNotice('Estado actualizado. Revisa el resultado de la operación.');
              onSaved();
            }}
          />
          {!closed && !unpaid && !resolving && (
            <form onSubmit={save}>
              <fieldset disabled={busy} className="space-y-4 border-t border-stone-200 pt-5">
                <h3 className="font-semibold">Actualizar entrega</h3>
                <label className="block text-sm">
                  Estado
                  <select
                    className={input}
                    value={draft.status}
                    onChange={(e) => setDraft({ ...draft, status: e.target.value })}
                  >
                    {[order.fulfillmentStatus, ...(next[order.fulfillmentStatus] || [])].map((s) => (
                      <option key={s} value={s}>
                        {states[s]}
                      </option>
                    ))}
                  </select>
                </label>
                <div className="grid gap-4 sm:grid-cols-2">
                  <label className="text-sm">
                    Paquetería (opcional)
                    <input
                      maxLength={100}
                      className={input}
                      value={draft.carrier}
                      onChange={(e) => setDraft({ ...draft, carrier: e.target.value })}
                    />
                  </label>
                  <label className="text-sm">
                    Número de guía (opcional)
                    <input
                      maxLength={100}
                      className={input}
                      value={draft.trackingNumber}
                      onChange={(e) => setDraft({ ...draft, trackingNumber: e.target.value })}
                    />
                  </label>
                </div>
                <p className="text-sm text-stone-500">
                  Captura ambos campos para paquetería. Para entrega local o recogida puedes dejarlos vacíos y pasar de
                  preparando a entregado.
                </p>
                <button className={button}>{busy ? 'Guardando…' : 'Guardar entrega'}</button>
              </fieldset>
            </form>
          )}
        </>
      )}
    </section>
  );
}

export default function AdminOrders({ session, initialFilters = {}, initialOrderId = null }) {
  const [draft, setDraft] = useState(() => ({ ...empty, ...initialFilters }));
  const [filters, setFilters] = useState(() => ({ ...empty, ...initialFilters }));
  const [page, setPage] = useState(1);
  const [result, setResult] = useState(null);
  const [selected, setSelected] = useState(initialOrderId);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let current = true;
    const query = new URLSearchParams({ page: String(page) });
    Object.entries(filters).forEach(([key, value]) => {
      if (value) query.set(key, value);
    });
    setLoading(true);
    setError('');
    setResult(null);
    getAdminOrders(session, query)
      .then((r) => {
        if (current) setResult(r.data);
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
  }, [session, filters, page, revision]);
  if (selected)
    return (
      <Detail
        key={selected}
        session={session}
        id={selected}
        onClose={() => setSelected(null)}
        onSaved={() => setRevision((n) => n + 1)}
      />
    );
  return (
    <section>
      <div className="mb-4 flex items-center justify-between gap-3">
        <h2 className="text-xl font-semibold">Pedidos de tu tienda</h2>
        <button disabled={loading} onClick={() => setRevision((n) => n + 1)} className="underline">
          Actualizar
        </button>
      </div>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          setPage(1);
          setFilters({ ...draft });
        }}
        className="mb-5 space-y-4 rounded-xl border border-stone-200 bg-white p-5"
      >
        <label className="block text-sm">
          Buscar por folio, cliente o correo
          <input
            type="search"
            maxLength={100}
            className={input}
            value={draft.search}
            onChange={(e) => setDraft({ ...draft, search: e.target.value })}
          />
        </label>
        <div className="grid gap-3 sm:grid-cols-3">
          {[
            ['paymentMethod', 'Forma de pago', methods],
            ['paymentStatus', 'Estado de pago', payments],
            ['fulfillmentStatus', 'Estado de entrega', states],
          ].map(([key, label, options]) => (
            <label key={key} className="text-sm">
              {label}
              <select
                className={input}
                value={draft[key]}
                onChange={(e) => setDraft({ ...draft, [key]: e.target.value })}
              >
                <option value="">Todos</option>
                {Object.entries(options).map(([value, name]) => (
                  <option key={value} value={value}>
                    {name}
                  </option>
                ))}
              </select>
            </label>
          ))}
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="text-sm">
            Desde (UTC)
            <input
              type="date"
              max={draft.to || '9998-12-31'}
              className={input}
              value={draft.from}
              onChange={(e) => setDraft({ ...draft, from: e.target.value })}
            />
          </label>
          <label className="text-sm">
            Hasta (UTC, incluido)
            <input
              type="date"
              min={draft.from}
              max="9998-12-31"
              className={input}
              value={draft.to}
              onChange={(e) => setDraft({ ...draft, to: e.target.value })}
            />
          </label>
        </div>
        <div className="flex gap-4">
          <button className={button}>Aplicar filtros</button>
          <button
            type="button"
            onClick={() => {
              setDraft(empty);
              setFilters(empty);
              setPage(1);
            }}
            className="underline"
          >
            Limpiar
          </button>
        </div>
      </form>
      {error && (
        <p role="alert" className="mb-4 rounded-lg bg-red-50 p-4 text-red-800">
          {error}
        </p>
      )}
      {loading && <p role="status">Cargando pedidos…</p>}
      {result && (
        <>
          <p className="mb-3 text-sm text-stone-500">{result.totalCount} pedidos encontrados</p>
          <ul className="space-y-3">
            {result.items.map((order) => (
              <li key={order.id} className="rounded-xl border border-stone-200 bg-white p-5">
                <div className="flex flex-wrap justify-between gap-3">
                  <div className="min-w-0">
                    <h3 className="break-all font-semibold">{order.number}</h3>
                    <p className="mt-1">
                      {order.firstName} {order.lastName}
                    </p>
                    <p className="break-all text-sm text-stone-500">{order.email}</p>
                    <p className="mt-1 text-xs text-stone-500">{date(order.createdAtUtc)} UTC</p>
                  </div>
                  <div>
                    <p className="font-semibold">
                      {order.total == null ? 'Total por acordar' : formatMXN(order.total)}
                    </p>
                    <p className="text-sm">{methods[order.paymentMethod]}</p>
                  </div>
                </div>
                <div className="mt-4 flex flex-wrap items-center gap-3">
                  <span className="rounded-full bg-stone-100 px-3 py-1 text-xs">
                    Pago: {payments[order.paymentStatus]}
                  </span>
                  <span className="rounded-full bg-stone-100 px-3 py-1 text-xs">
                    Entrega: {states[order.fulfillmentStatus]}
                  </span>
                  <button onClick={() => setSelected(order.id)} className="ml-auto p-2 text-sm font-semibold underline">
                    Ver pedido
                  </button>
                </div>
              </li>
            ))}
          </ul>
          {!result.items.length && (
            <p className="rounded-xl bg-stone-50 p-6">No hay pedidos en esta página con los filtros seleccionados.</p>
          )}
          <div className="mt-5 flex items-center justify-between gap-3">
            <button
              disabled={page === 1}
              onClick={() => setPage((p) => p - 1)}
              className="rounded-lg border px-4 py-2 disabled:opacity-40"
            >
              Anterior
            </button>
            <p className="text-sm">
              Página {page} de {Math.max(1, Math.ceil(result.totalCount / result.pageSize))}
            </p>
            <button
              disabled={page * result.pageSize >= result.totalCount}
              onClick={() => setPage((p) => p + 1)}
              className="rounded-lg border px-4 py-2 disabled:opacity-40"
            >
              Siguiente
            </button>
          </div>
        </>
      )}
    </section>
  );
}
