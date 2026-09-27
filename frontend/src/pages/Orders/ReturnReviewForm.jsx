import { useState } from 'react';

const input = 'mt-2 w-full rounded-xl border border-stone-300 bg-white p-3';
const button = 'rounded-xl bg-brand px-4 py-3 text-sm font-semibold text-white disabled:opacity-40';

export default function ReturnReviewForm({ request, disabled, onReview }) {
  const [note, setNote] = useState('');
  const [status, setStatus] = useState('Approved');
  return (
    <form
      className="mt-4"
      onSubmit={(event) => {
        event.preventDefault();
        onReview(request, status, note);
      }}
    >
      <fieldset disabled={disabled} className="space-y-3">
        <label className="block text-sm">
          Decisión
          <select className={input} value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="Approved">Aprobar devolución</option>
            <option value="Rejected">Rechazar solicitud</option>
          </select>
        </label>
        <label className="block text-sm">
          Respuesta para el cliente
          <textarea
            required
            maxLength={1000}
            rows={3}
            className={input}
            value={note}
            onChange={(event) => setNote(event.target.value)}
            placeholder="Explica la decisión e indica cómo coordinar la devolución."
          />
        </label>
        <p className="text-xs text-stone-500">
          Esta decisión quedará registrada. Aprobar no confirma la recepción ni devuelve dinero.
        </p>
        <button className={button}>Guardar decisión</button>
      </fieldset>
    </form>
  );
}
