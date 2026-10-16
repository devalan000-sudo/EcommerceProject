import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api/client.js';
import { errorMessage } from '../../services/api/errors.js';
import { formatOrderDate } from '../../utils/orders/orders.js';
import ReturnReceiptForm from './ReturnReceiptForm.jsx';
import ReturnRefunds from './ReturnRefunds.jsx';
import { createReturn, getReturnOptions, listReturnRequests, reviewReturn } from '../../services/orders/ordersService.js';
import ReturnReviewForm from './ReturnReviewForm.jsx';

const labels = {
  Requested: 'Pendiente de revisión',
  Approved: 'Aprobada · Pendiente de recepción',
  Rejected: 'Rechazada',
  PartiallyReceived: 'Recibida parcialmente',
  Received: 'Recepción completa',
};
const input = 'mt-2 w-full rounded-xl border border-stone-300 bg-white p-3';
const button = 'rounded-xl bg-brand px-4 py-3 text-sm font-semibold text-white disabled:opacity-40';

export default function ReturnRequests({ session, orderId, admin = false, onExpired, onBusy, onOrder }) {
  const [data, setData] = useState(null);
  const [options, setOptions] = useState(null);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('Requested');
  const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [reason, setReason] = useState('');
  const [quantities, setQuantities] = useState({});
  const lock = useRef(false);
  // Conserva la clave y el contenido exactos si se pierde la respuesta al crear.
  const pending = useRef(null);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    Promise.all([
      listReturnRequests(api, session, { orderId, admin, page, status, signal: controller.signal }),
      admin ? Promise.resolve(null) : getReturnOptions(api, session, orderId, controller.signal),
    ])
      .then(([list, eligible]) => {
        if (!controller.signal.aborted) {
          setData(list.data);
          setOptions(eligible?.data);
        }
      })
      .catch((e) => {
        if (!controller.signal.aborted) {
          setError(errorMessage(e));
          if ([401, 403].includes(e.response?.status)) onExpired?.();
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [session.token, admin, orderId, page, status, revision, onExpired]);

  async function mutate(operation, success) {
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    onBusy?.(true);
    setError('');
    setNotice('');
    try {
      await operation();
      success?.();
      setRevision((n) => n + 1);
    } catch (e) {
      setError(errorMessage(e));
      if ([401, 403].includes(e.response?.status)) onExpired?.();
    } finally {
      lock.current = false;
      setBusy(false);
      onBusy?.(false);
    }
  }
  function create(e) {
    e.preventDefault();
    const items = Object.entries(quantities)
      .filter(([, quantity]) => Number(quantity) > 0)
      .map(([id, quantity]) => ({ orderItemId: Number(id), quantity: Number(quantity) }));
    if (!items.length && !pending.current) {
      setError('Selecciona al menos un artículo y su cantidad.');
      return;
    }
    if (!pending.current) pending.current = { requestId: crypto.randomUUID(), reason, items };
    mutate(
      () => createReturn(api, session, orderId, pending.current),
      () => {
        pending.current = null;
        setReason('');
        setQuantities({});
        setPage(1);
        setNotice('Solicitud enviada. El negocio revisará tu devolución.');
      },
    );
  }
  function review(request, nextStatus, note) {
    mutate(
      () =>
        reviewReturn(api, session, request.id, { version: request.version, status: nextStatus, decisionNote: note }),
      () => setNotice('Decisión guardada. El cliente puede consultarla en su pedido.'),
    );
  }
  return (
    <section
      aria-label="Devoluciones"
      className="mt-7 space-y-5 rounded-2xl border border-stone-200 bg-stone-50 p-4 sm:p-6"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-semibold">{admin ? 'Solicitudes de devolución' : 'Devoluciones de este pedido'}</h2>
        <button
          disabled={busy || loading}
          onClick={() => setRevision((n) => n + 1)}
          className="text-sm underline disabled:opacity-40"
        >
          Actualizar
        </button>
      </div>
      <p className="text-sm text-stone-600">
        La solicitud requiere revisión del negocio. La aprobación no confirma un reembolso.{' '}
        <a href="/informacion#devoluciones" className="underline">
          Consultar políticas
        </a>
      </p>
      {admin && (
        <label className="block text-sm">
          Estado
          <select
            disabled={busy}
            className={input}
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="">Todos</option>
            {Object.entries(labels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
      )}
      {error && (
        <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-800">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="rounded-xl bg-emerald-50 p-3">
          {notice}
        </p>
      )}
      {loading ? (
        <p role="status">Cargando devoluciones…</p>
      ) : (
        !error &&
        data && (
          <>
            {!data.items.length && <p className="text-sm">No hay solicitudes en esta página.</p>}
            <ul className="space-y-4">
              {data.items.map((request) => (
                <li key={request.id} className="rounded-xl border border-stone-200 bg-white p-4">
                  {admin && (
                    <button
                      disabled={busy}
                      onClick={() => onOrder?.(request.orderId)}
                      className="break-all text-sm underline"
                    >
                      Pedido {request.orderNumber}
                    </button>
                  )}
                  <p className="mt-2 font-semibold">{labels[request.status]}</p>
                  <p className="mt-1 text-xs text-stone-500">{formatOrderDate(request.createdAtUtc)}</p>
                  <ul className="my-3 space-y-1 text-sm">
                    {request.items.map((item) => (
                      <li key={item.orderItemId}>
                        {item.quantity} × {item.productName} {[item.size, item.color].filter(Boolean).join(' · ')}
                      </li>
                    ))}
                  </ul>
                  <p className="whitespace-pre-wrap break-words text-sm">
                    <strong>Motivo:</strong> {request.reason}
                  </p>
                  {request.decisionNote && (
                    <p className="mt-3 whitespace-pre-wrap break-words text-sm">
                      <strong>Respuesta del negocio:</strong> {request.decisionNote}
                    </p>
                  )}
                  {request.reviewedAtUtc && (
                    <p className="mt-2 text-xs text-stone-500">Revisada: {formatOrderDate(request.reviewedAtUtc)}</p>
                  )}
                  {request.receipts?.length > 0 && (
                    <div className="mt-4 rounded-xl bg-stone-50 p-3">
                      <h3 className="text-sm font-semibold">Artículos recibidos</h3>
                      <ul className="mt-2 space-y-2 text-sm">
                        {request.items.map((item) => (
                          <li key={item.orderItemId} className="break-words">
                            {item.productName} · {[item.size, item.color].filter(Boolean).join(' · ')}:{' '}
                            {item.receivedQuantity} de {item.quantity} recibidas
                            {admin && ` · ${item.restockedQuantity} repuestas`}
                          </li>
                        ))}
                      </ul>
                      <details className="mt-3 text-sm">
                        <summary className="cursor-pointer">Historial de recepciones</summary>
                        <ul className="mt-3 space-y-4">
                          {request.receipts.map((receipt) => (
                            <li key={receipt.id}>
                              <p className="text-xs text-stone-500">{formatOrderDate(receipt.createdAtUtc)}</p>
                              <p className="whitespace-pre-wrap break-words">{receipt.note}</p>
                              {receipt.items.map((item) => {
                                const product = request.items.find((i) => i.orderItemId === item.orderItemId);
                                return (
                                  <p key={item.orderItemId}>
                                    {product?.productName} ·{' '}
                                    {[product?.size, product?.color].filter(Boolean).join(' · ')}:{' '}
                                    {item.receivedQuantity} recibidas{admin && ` · ${item.restockedQuantity} repuestas`}
                                  </p>
                                );
                              })}
                            </li>
                          ))}
                        </ul>
                      </details>
                      <p className="mt-3 text-xs text-stone-500">
                        Este registro confirma la recepción de artículos, no un reembolso.
                      </p>
                    </div>
                  )}
                  {admin && request.status === 'Requested' && (
                    <ReturnReviewForm key={request.version} request={request} disabled={busy} onReview={review} />
                  )}
                  {(request.receipts?.length > 0 || request.refunds?.length > 0) && (
                    <ReturnRefunds
                      key={`refund-${request.version}`}
                      request={request}
                      admin={admin}
                      session={session}
                      disabled={busy}
                      onBusy={(value) => {
                        setBusy(value);
                        onBusy?.(value);
                      }}
                      onSaved={() => {
                        setNotice('Estado del reembolso actualizado. Consulta el resultado en el historial.');
                        setRevision((n) => n + 1);
                      }}
                    />
                  )}
                  {admin && ['Approved', 'PartiallyReceived'].includes(request.status) && (
                    <ReturnReceiptForm
                      key={request.version}
                      request={request}
                      session={session}
                      disabled={busy}
                      onBusy={(value) => {
                        setBusy(value);
                        onBusy?.(value);
                      }}
                      onSaved={() => {
                        setNotice('Recepción registrada. El inventario se actualizó para las unidades indicadas.');
                        setRevision((n) => n + 1);
                      }}
                    />
                  )}
                </li>
              ))}
            </ul>
            <nav aria-label="Páginas de devoluciones" className="flex items-center justify-between gap-3 text-sm">
              <button
                disabled={busy || page <= 1}
                onClick={() => setPage((n) => n - 1)}
                className="underline disabled:opacity-40"
              >
                Anterior
              </button>
              <span>Página {page}</span>
              <button
                disabled={busy || page * data.pageSize >= data.totalCount || page >= 100000}
                onClick={() => setPage((n) => n + 1)}
                className="underline disabled:opacity-40"
              >
                Siguiente
              </button>
            </nav>
          </>
        )
      )}
      {!admin && !loading && options && (
        <>
          {!options.canRequest && !pending.current ? (
            <p className="text-sm text-stone-600">{options.unavailableReason}</p>
          ) : (
            <form onSubmit={create} className="space-y-4 border-t border-stone-200 pt-5">
              <h3 className="font-semibold">Solicitar una devolución</h3>
              <fieldset disabled={busy || Boolean(pending.current)} className="space-y-4">
                {options.items
                  .filter((item) => item.quantity > 0)
                  .map((item) => (
                    <label key={item.orderItemId} className="block text-sm">
                      {item.productName} · {[item.size, item.color].filter(Boolean).join(' · ')} · Disponible:{' '}
                      {item.quantity}
                      <input
                        type="number"
                        min={0}
                        max={item.quantity}
                        step={1}
                        value={quantities[item.orderItemId] ?? 0}
                        onChange={(e) =>
                          setQuantities((current) => ({ ...current, [item.orderItemId]: e.target.value }))
                        }
                        className={input}
                      />
                    </label>
                  ))}
                <label className="block text-sm">
                  Motivo de la devolución
                  <textarea
                    required
                    rows={3}
                    maxLength={1000}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    className={input}
                  />
                </label>
              </fieldset>
              {pending.current && (
                <p className="text-sm">
                  Hay un envío sin confirmar. Puedes reintentar con los mismos datos o actualizar para consultar si se
                  guardó.
                </p>
              )}
              <button disabled={busy} className={button}>
                {busy ? 'Enviando…' : pending.current ? 'Reintentar solicitud' : 'Enviar solicitud'}
              </button>
              {pending.current && (
                <button
                  type="button"
                  disabled={busy}
                  className="ml-4 text-sm underline"
                  onClick={() => {
                    pending.current = null;
                    setRevision((n) => n + 1);
                  }}
                >
                  Revisar solicitudes y editar
                </button>
              )}
            </form>
          )}
        </>
      )}
    </section>
  );
}
