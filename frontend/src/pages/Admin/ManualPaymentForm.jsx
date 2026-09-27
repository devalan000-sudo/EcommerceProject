import { useRef, useState } from 'react';
import { errorMessage } from '../../services/api/errors.js';
import { formatMXN } from '../../utils/money/money.js';
import { recordManualPayment } from '../../services/admin/adminService.js';

export default function ManualPaymentForm({ session, order, disabled, onBusy, onConfirmed }) {
  const [form, setForm] = useState({
    method: 'Transfer',
    reference: '',
    shippingName: order.shippingName || '',
    shippingCost: order.shippingCost ?? '',
    amountReceived: order.total ?? '',
  });
  const [checked, setChecked] = useState(false);
  const [error, setError] = useState('');
  const lock = useRef(false);
  const retry = useRef(null);
  const total = form.shippingCost === '' ? null : Math.round((order.subtotal + Number(form.shippingCost)) * 100) / 100;
  const legacy = order.items.some((i) => !i.variantId || !i.productId);
  async function confirm(event) {
    event.preventDefault();
    if (lock.current || !checked) return;
    lock.current = true;
    onBusy(true);
    setError('');
    try {
      const payload = {
        ...form,
        shippingCost: Number(form.shippingCost),
        amountReceived: Number(form.amountReceived),
        version: order.fulfillmentVersion,
      };
      const signature = JSON.stringify(payload);
      if (retry.current?.signature !== signature) retry.current = { signature, requestId: crypto.randomUUID() };
      const { data } = await recordManualPayment(session, order.id, { ...payload, requestId: retry.current.requestId });
      onConfirmed(data);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      lock.current = false;
      onBusy(false);
    }
  }
  if (legacy)
    return (
      <p className="my-5 rounded-xl bg-amber-50 p-4 text-sm">
        Este pedido contiene artículos sin variante o eliminados. Para registrar el pago y descontar existencias, crea
        un pedido nuevo con las tallas y colores acordados.
      </p>
    );
  return (
    <form onSubmit={confirm} className="my-5 rounded-xl border border-brand/20 p-5">
      <h3 className="font-semibold">Registrar pago de WhatsApp</h3>
      <p className="mt-2 text-sm text-stone-500">
        Registra únicamente un pago que ya recibiste. Esta acción marcará el pedido pagado y descontará las existencias
        disponibles.
      </p>
      {error && (
        <p role="alert" className="my-3 text-sm text-red-700">
          {error} Si la respuesta fue incierta, actualiza el detalle antes de volver a confirmar.
        </p>
      )}
      <fieldset disabled={disabled} className="mt-4 space-y-4">
        <label className="block text-sm">
          Medio de pago
          <select
            className="mt-1 w-full rounded-lg border p-3"
            value={form.method}
            onChange={(e) => setForm({ ...form, method: e.target.value })}
          >
            <option value="Transfer">Transferencia</option>
            <option value="Cash">Efectivo</option>
            <option value="Other">Otro</option>
          </select>
        </label>
        <label className="block text-sm">
          Referencia interna (opcional)
          <input
            maxLength={200}
            className="mt-1 w-full rounded-lg border p-3"
            value={form.reference}
            onChange={(e) => setForm({ ...form, reference: e.target.value })}
          />
        </label>
        <label className="block text-sm">
          Entrega acordada
          <input
            required
            maxLength={100}
            readOnly={order.shippingCost != null}
            className="mt-1 w-full rounded-lg border p-3"
            value={form.shippingName}
            onChange={(e) => setForm({ ...form, shippingName: e.target.value })}
          />
        </label>
        <label className="block text-sm">
          Costo de entrega MXN
          <input
            required
            type="number"
            min="0"
            max="999999.99"
            step="0.01"
            readOnly={order.shippingCost != null}
            className="mt-1 w-full rounded-lg border p-3"
            value={form.shippingCost}
            onChange={(e) => setForm({ ...form, shippingCost: e.target.value })}
          />
        </label>
        <p className="font-semibold">Total a confirmar: {total == null ? 'Completa la entrega' : formatMXN(total)}</p>
        <label className="block text-sm">
          Importe recibido MXN
          <input
            required
            type="number"
            min="0.01"
            step="0.01"
            className="mt-1 w-full rounded-lg border p-3"
            value={form.amountReceived}
            onChange={(e) => setForm({ ...form, amountReceived: e.target.value })}
          />
        </label>
        <label className="flex gap-3 text-sm">
          <input required type="checkbox" checked={checked} onChange={(e) => setChecked(e.target.checked)} />
          Confirmo que recibí el total y verifiqué las tallas, colores y entrega con el cliente.
        </label>
        <button
          disabled={!checked}
          className="rounded-lg bg-brand px-4 py-3 font-semibold text-white disabled:opacity-40"
        >
          Confirmar pago y descontar inventario
        </button>
      </fieldset>
    </form>
  );
}
