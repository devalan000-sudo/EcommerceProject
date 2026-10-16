import { useRef, useState } from 'react';
import { resolveOrder } from '../../services/orders/ordersService.js';
import { api } from '../../services/api/client.js';
import { errorMessage } from '../../services/api/errors.js';
import { formatMXN } from '../../utils/money/money.js';
import { resolutionAction, refundLabels } from '../../utils/orders/orders.js';

export default function OrderResolutionPanel({ order, session, busy, onBusy, onUpdated }) {
  const [reason, setReason] = useState('');
  const [restock, setRestock] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState('');
  const lock = useRef(false);
  const retry = useRef(null);
  const action = resolutionAction(order);
  async function submit(event, operation = action) {
    event?.preventDefault();
    if (lock.current) return;
    lock.current = true;
    onBusy(true);
    setError('');
    try {
      let payload = { version: order.fulfillmentVersion, reason: reason || 'Reintento de cancelación solicitada' };
      if (operation === 'refund') {
        payload = { ...payload, restock, manualRefundConfirmed: order.paymentMethod === 'WhatsApp' && confirmed };
        const signature = JSON.stringify(payload);
        if (retry.current?.signature !== signature) retry.current = { signature, requestId: crypto.randomUUID() };
        payload.requestId = retry.current.requestId;
      }
      const { data } = await resolveOrder(api, session, order.id, operation, payload);
      onUpdated(data);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      lock.current = false;
      onBusy(false);
    }
  }
  return (
    <section className="my-6 rounded-xl border border-stone-200 p-5">
      <h3 className="font-semibold">Cancelación y devolución</h3>
      {error && (
        <p role="alert" className="my-3 text-sm text-red-700">
          {error} Actualiza el detalle antes de repetir una operación incierta.
        </p>
      )}
      {order.cancellationPending && (
        <div className="mt-3 text-sm">
          <p>
            Cancelación pendiente: se conserva la reserva hasta cerrar la sesión Stripe. Si el pago se completó antes,
            quedará pagado y deberás solicitar un reembolso.
          </p>
          <button disabled={busy} onClick={(e) => submit(e, 'cancel')} className="mt-3 underline">
            Consultar cancelación
          </button>
        </div>
      )}
      {order.refundStatus && (
        <div className="mt-3 text-sm">
          <p>
            Reembolso: <strong>{refundLabels[order.refundStatus] || order.refundStatus}</strong>
          </p>
          {order.refundStatus !== 'Succeeded' && (
            <>
              <p className="mt-2">
                La devolución aún no está confirmada. No hagas otra devolución desde el panel de Stripe sin revisar esta
                solicitud.
              </p>
              <button disabled={busy} onClick={(e) => submit(e, 'refund/refresh')} className="mt-3 underline">
                Consultar resultado en Stripe
              </button>
            </>
          )}
          {order.refundStatus === 'Succeeded' && (
            <p className="mt-2">
              {order.restockedAtUtc
                ? 'Existencias repuestas.'
                : order.refundRestock
                  ? 'Dinero devuelto; reposición pendiente de revisar las variantes.'
                  : 'Sin reposición de existencias, según lo indicado al confirmar.'}
            </p>
          )}
          {order.refundStatus === 'Succeeded' && order.refundRestock && !order.restockedAtUtc && (
            <button disabled={busy} onClick={(e) => submit(e, 'refund/restock')} className="mt-3 underline">
              Reintentar reposición autorizada
            </button>
          )}
        </div>
      )}
      {action && !order.cancellationPending && (
        <form onSubmit={submit} className="mt-4">
          <fieldset disabled={busy} className="space-y-4">
            <p className="text-sm">
              {action === 'refund'
                ? `Devolución total: ${formatMXN(order.total)} MXN, incluida la entrega.`
                : 'Cancelar el pedido sin pago. No se sumarán existencias que nunca se descontaron.'}
            </p>
            <label className="block text-sm">
              Motivo
              <input
                required
                maxLength={500}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                className="mt-1 w-full rounded-lg border p-3"
              />
            </label>
            {action === 'refund' && (
              <label className="flex gap-3 text-sm">
                <input type="checkbox" checked={restock} onChange={(e) => setRestock(e.target.checked)} />
                Reponer todos los artículos: confirmo que siguen en la tienda y están disponibles para vender.
              </label>
            )}
            <label className="flex gap-3 text-sm">
              <input required type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />
              {action === 'cancel'
                ? 'Confirmo la cancelación de este pedido.'
                : order.paymentMethod === 'WhatsApp'
                  ? 'Ya devolví al cliente el importe completo por fuera del sistema.'
                  : 'Confirmo solicitar a Stripe el reembolso total de prueba.'}
            </label>
            <button
              disabled={!confirmed}
              className="rounded-lg bg-red-800 px-4 py-3 text-sm font-semibold text-white disabled:opacity-40"
            >
              {busy
                ? 'Procesando…'
                : action === 'cancel'
                  ? 'Cancelar pedido'
                  : order.paymentMethod === 'WhatsApp'
                    ? 'Registrar devolución realizada'
                    : 'Solicitar reembolso de prueba'}
            </button>
          </fieldset>
        </form>
      )}
      {!action && !order.refundStatus && !order.cancellationPending && (
        <p className="mt-3 text-sm text-stone-500">
          {['Shipped', 'Delivered'].includes(order.fulfillmentStatus)
            ? 'El pedido ya salió. Las devoluciones con recepción física se habilitarán en otra etapa.'
            : 'No hay una cancelación o devolución disponible para este estado.'}
        </p>
      )}
    </section>
  );
}
