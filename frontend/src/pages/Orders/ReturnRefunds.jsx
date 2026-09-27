import { useRef, useState } from 'react';
import { api } from '../../services/api/client.js';
import { errorMessage } from '../../services/api/errors.js';
import { formatMXN } from '../../utils/money/money.js';
import { formatOrderDate } from '../../utils/orders/orders.js';
import { refundableQuantity, refundAmount } from '../../utils/returnRefunds/returnRefunds.js';
import { createReturnRefund } from '../../services/orders/ordersService.js';

const labels = {
  Pending: 'Pendiente de confirmación',
  Succeeded: 'Confirmado',
  Failed: 'Fallido · No se devolvió el dinero',
  NeedsReview: 'Requiere revisión',
};
const input = 'mt-2 w-full rounded-xl border border-stone-300 bg-white p-3';
const button = 'rounded-xl bg-brand px-4 py-3 text-sm font-semibold text-white disabled:opacity-40';

export default function ReturnRefunds({ request, admin, session, disabled, onBusy, onSaved }) {
  const [quantities, setQuantities] = useState({});
  const [note, setNote] = useState('');
  const [reference, setReference] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [recovery, setRecovery] = useState({});
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const lock = useRef(false);
  const pending = useRef(null);
  const available = request.items.filter((i) => refundableQuantity(request, i) > 0);
  const manual = request.paymentMethod === 'WhatsApp';
  async function send(path, payload, creating) {
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    onBusy?.(true);
    setError('');
    try {
      await createReturnRefund(api, session, request.id, payload);
      pending.current = null;
      onSaved();
    } catch (e) {
      setError(errorMessage(e));
      if (creating && e.response?.status === 400) pending.current = null;
    } finally {
      lock.current = false;
      setBusy(false);
      onBusy?.(false);
    }
  }
  function submit(e) {
    e.preventDefault();
    if (!pending.current) {
      const items = available
        .filter((i) => Number(quantities[i.orderItemId]) > 0)
        .map((i) => ({ orderItemId: i.orderItemId, quantity: Number(quantities[i.orderItemId]) }));
      if (!items.length) {
        setError('Selecciona unidades recibidas para reembolsar.');
        return;
      }
      pending.current = {
        requestId: crypto.randomUUID(),
        version: request.version,
        note,
        manualReference: reference,
        manualRefundConfirmed: confirmed,
        expectedAmount: refundAmount(available, quantities),
        items,
      };
    }
    send(`/admin/returns/${request.id}/refunds`, pending.current, true);
  }
  return (
    <section className="mt-5 border-t border-stone-200 pt-4" aria-label="Reembolsos de la devolución">
      <h3 className="font-semibold">Reembolsos</h3>
      <p className="mt-2 text-sm text-stone-600">
        Se devuelven los artículos recibidos a su precio original. El envío no está incluido. El pago original permanece
        registrado; aquí puedes consultar el dinero devuelto.
      </p>
      {error && (
        <p role="alert" className="my-3 rounded-xl bg-red-50 p-3 text-red-800">
          {error}
        </p>
      )}
      {!request.refunds?.length && <p className="mt-3 text-sm">Aún no hay reembolsos registrados.</p>}
      <ul className="mt-3 space-y-4">
        {request.refunds?.map((refund) => (
          <li key={refund.id} className="rounded-xl bg-stone-50 p-3 text-sm">
            <p className="font-semibold">
              {formatMXN(refund.amount)} MXN · {labels[refund.status]}
            </p>
            <p className="mt-1 text-xs text-stone-500">
              Solicitado: {formatOrderDate(refund.createdAtUtc)}
              {refund.completedAtUtc && ` · Resultado: ${formatOrderDate(refund.completedAtUtc)}`}
            </p>
            <p className="mt-2 whitespace-pre-wrap break-words">{refund.note}</p>
            {refund.items.map((item) => {
              const product = request.items.find((i) => i.orderItemId === item.orderItemId);
              return (
                <p key={item.orderItemId}>
                  {item.quantity} × {product?.productName} ·{' '}
                  {[product?.size, product?.color].filter(Boolean).join(' · ')}
                </p>
              );
            })}
            {admin && !manual && ['Pending', 'NeedsReview'].includes(refund.status) && (
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  send(
                    `/admin/return-refunds/${refund.id}/refresh`,
                    { stripeRefundId: recovery[refund.id]?.trim() || null },
                    false,
                  );
                }}
                className="mt-3"
              >
                {refund.status === 'NeedsReview' && (
                  <label className="block">
                    ID del reembolso en Stripe (opcional)
                    <input
                      className={input}
                      maxLength={255}
                      pattern="re_[a-zA-Z0-9]+"
                      placeholder="re_..."
                      value={recovery[refund.id] || ''}
                      onChange={(e) => setRecovery((current) => ({ ...current, [refund.id]: e.target.value }))}
                    />
                    <span className="text-xs">
                      Recupera el reembolso original, no crees otro. El sistema comprobará importe, pago y metadatos.
                    </span>
                  </label>
                )}
                <button disabled={disabled || busy} className="mt-3 underline disabled:opacity-40">
                  Consultar resultado en Stripe
                </button>
              </form>
            )}
          </li>
        ))}
      </ul>
      {admin && available.length > 0 && (
        <form onSubmit={submit} className="mt-5 space-y-4">
          <h4 className="font-semibold">
            {manual ? 'Registrar devolución de dinero externa' : 'Solicitar reembolso en Stripe de prueba'}
          </h4>
          <fieldset disabled={disabled || busy || Boolean(pending.current)} className="space-y-4">
            {available.map((item) => (
              <label key={item.orderItemId} className="block text-sm">
                {item.productName} · {[item.size, item.color].filter(Boolean).join(' · ')} · {formatMXN(item.unitPrice)}{' '}
                c/u · Disponibles: {refundableQuantity(request, item)}
                <input
                  className={input}
                  type="number"
                  min={0}
                  max={refundableQuantity(request, item)}
                  step={1}
                  value={quantities[item.orderItemId] ?? 0}
                  onChange={(e) => setQuantities((current) => ({ ...current, [item.orderItemId]: e.target.value }))}
                />
              </label>
            ))}
            <label className="block text-sm">
              Nota visible para el cliente
              <textarea
                className={input}
                required
                maxLength={1000}
                rows={2}
                value={note}
                onChange={(e) => setNote(e.target.value)}
              />
            </label>
            {manual && (
              <>
                <label className="block text-sm">
                  Referencia del reembolso externo
                  <input
                    className={input}
                    required
                    maxLength={200}
                    value={reference}
                    onChange={(e) => setReference(e.target.value)}
                  />
                </label>
                <label className="flex items-start gap-3 text-sm">
                  <input
                    required
                    type="checkbox"
                    checked={confirmed}
                    onChange={(e) => setConfirmed(e.target.checked)}
                  />
                  Confirmo que ya devolví este importe por fuera del sistema. Este registro no transfiere dinero.
                </label>
              </>
            )}
          </fieldset>
          <p className="font-semibold">Importe: {formatMXN(refundAmount(available, quantities))} MXN</p>
          {pending.current && (
            <p className="text-sm">
              Respuesta sin confirmar. Reintenta la misma operación o actualiza el historial antes de continuar.
            </p>
          )}
          <button disabled={disabled || busy} className={button}>
            {busy
              ? 'Procesando…'
              : pending.current
                ? 'Reintentar la misma operación'
                : manual
                  ? 'Registrar reembolso realizado'
                  : 'Solicitar reembolso'}
          </button>
        </form>
      )}
    </section>
  );
}
