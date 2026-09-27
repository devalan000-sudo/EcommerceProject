import { useRef, useState } from 'react';
import { api } from '../../services/api/client.js';
import { errorMessage } from '../../services/api/errors.js';
import { registerReturnReceipt } from '../../services/orders/ordersService.js';

export default function ReturnReceiptForm({ request, session, disabled, onBusy, onSaved }) {
  const [quantities, setQuantities] = useState({});
  const [note, setNote] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const pending = useRef(null);
  const lock = useRef(false);
  const inputClass = 'mt-2 w-full rounded-xl border border-stone-300 bg-white p-3';
  function change(id, field, value) {
    setQuantities((current) => ({ ...current, [id]: { ...current[id], [field]: value } }));
  }
  async function submit(e) {
    e.preventDefault();
    if (lock.current) return;
    if (!pending.current) {
      const items = request.items
        .map((item) => ({
          orderItemId: item.orderItemId,
          receivedQuantity: Number(quantities[item.orderItemId]?.received || 0),
          restockedQuantity: Number(quantities[item.orderItemId]?.restocked || 0),
        }))
        .filter((i) => i.receivedQuantity || i.restockedQuantity);
      if (!items.length) {
        setError('Indica al menos una unidad recibida.');
        return;
      }
      pending.current = { requestId: crypto.randomUUID(), version: request.version, note, items };
    }
    lock.current = true;
    setBusy(true);
    onBusy(true);
    setError('');
    try {
      await registerReturnReceipt(api, session, request.id, pending.current);
      pending.current = null;
      onSaved();
    } catch (e) {
      setError(errorMessage(e));
      // Un 400 confirma que la operación fue rechazada antes de guardar.
      if (e.response?.status === 400) pending.current = null;
    } finally {
      lock.current = false;
      setBusy(false);
      onBusy(false);
    }
  }
  return (
    <form className="mt-5 border-t border-stone-200 pt-4" onSubmit={submit}>
      <h3 className="font-semibold">Registrar recepción</h3>
      <p className="mt-2 text-sm text-stone-600">
        Introduce solo las unidades de esta entrega. Reponer agrega existencias a la talla y color originales. Deja cero
        si no están aptas para venderse.
      </p>
      {error && (
        <p role="alert" className="my-3 rounded-xl bg-red-50 p-3 text-red-800">
          {error}
        </p>
      )}
      <fieldset disabled={disabled || busy || Boolean(pending.current)} className="mt-4 space-y-4">
        {request.items
          .filter((i) => i.quantity > i.receivedQuantity)
          .map((item) => (
            <div key={item.orderItemId}>
              <p className="break-words text-sm font-medium">
                {item.productName} · {[item.size, item.color].filter(Boolean).join(' · ')} · Pendientes:{' '}
                {item.quantity - item.receivedQuantity}
              </p>
              <div className="grid gap-3 sm:grid-cols-2">
                <label className="text-sm">
                  Recibidas ahora
                  <input
                    className={inputClass}
                    type="number"
                    min={0}
                    max={item.quantity - item.receivedQuantity}
                    step={1}
                    value={quantities[item.orderItemId]?.received ?? 0}
                    onChange={(e) => change(item.orderItemId, 'received', e.target.value)}
                  />
                </label>
                <label className="text-sm">
                  De estas, reponer
                  <input
                    className={inputClass}
                    type="number"
                    min={0}
                    max={Number(quantities[item.orderItemId]?.received || 0)}
                    step={1}
                    value={quantities[item.orderItemId]?.restocked ?? 0}
                    onChange={(e) => change(item.orderItemId, 'restocked', e.target.value)}
                  />
                </label>
              </div>
            </div>
          ))}
        <label className="block text-sm">
          Condición y observaciones (visibles para el cliente)
          <textarea
            className={inputClass}
            required
            maxLength={1000}
            rows={3}
            value={note}
            onChange={(e) => setNote(e.target.value)}
            placeholder="Ej.: una unidad en buen estado; otra con daño, sin reposición."
          />
        </label>
      </fieldset>
      <p className="my-3 text-xs text-stone-500">
        El registro no se puede editar. La recepción y la reposición se guardan juntas; no emiten reembolsos. Las
        unidades recibidas sin reposición no podrán reponerse después desde este flujo.
      </p>
      {pending.current && (
        <p className="my-3 text-sm">
          Respuesta sin confirmar. Reintenta con los mismos datos o pulsa Actualizar para consultar el historial antes
          de registrar otra entrega.
        </p>
      )}
      <button
        disabled={disabled || busy}
        className="rounded-xl bg-brand px-4 py-3 text-sm font-semibold text-white disabled:opacity-40"
      >
        {busy ? 'Guardando…' : pending.current ? 'Reintentar recepción' : 'Confirmar recepción y reposición'}
      </button>
    </form>
  );
}
