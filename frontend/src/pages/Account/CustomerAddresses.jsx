import DeliveryAddressFields from '../../components/forms/DeliveryAddressFields.jsx';
import { getAddresses } from '../../services/account/accountService.js';
import { validateAddressForm } from '../../utils/addresses/addresses.js';
import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api/client.js';
import { errorMessage } from '../../services/api/errors.js';

const empty = {
  label: '',
  firstName: '',
  lastName: '',
  phone: '',
  street: '',
  exteriorNumber: '',
  interiorNumber: '',
  neighborhood: '',
  city: '',
  state: '',
  postalCode: '',
  country: 'México',
  references: '',
  isDefault: false,
};
const fields = [
  ['label', 'Nombre de la dirección (Casa, Trabajo…)', 40, true, 'off'],
  ['firstName', 'Nombre de quien recibe', 100, true, 'given-name'],
  ['lastName', 'Apellido de quien recibe', 100, true, 'family-name'],
  ['phone', 'Teléfono de contacto (opcional)', 25, false, 'tel'],
];
const input = 'mt-2 w-full rounded-xl border border-stone-300 bg-white p-3';
const button = 'rounded-xl bg-brand px-5 py-3 text-sm font-semibold text-white disabled:opacity-40';

export default function CustomerAddresses({ session, onExpired }) {
  const [items, setItems] = useState([]);
  const [fieldErrors, setFieldErrors] = useState({});
  const [draft, setDraft] = useState(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [revision, setRevision] = useState(0);
  const lock = useRef(false);
  const headers = { Authorization: `Bearer ${session.token}` };
  const lockedDefault = draft?.id && items.find((a) => a.id === draft.id)?.isDefault;
  function handleError(e) {
    if (e.response?.status === 401 || e.response?.status === 403) onExpired();
    else setError(errorMessage(e));
  }
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    getAddresses(api, session, controller.signal)
      .then(({ data }) => {
        if (!controller.signal.aborted) setItems(data);
      })
      .catch((e) => {
        if (!controller.signal.aborted) handleError(e);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [session.token, revision, onExpired]);
  async function mutate(method, address, message) {
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await api.request({
        method,
        url: `/customers/addresses${address.id ? `/${address.id}` : ''}`,
        headers,
        data: method === 'delete' ? { version: address.version } : { ...address, phone: address.phone?.trim() || null },
      });
      setDraft(null);
      setNotice(message);
      setLoading(true);
      setRevision((n) => n + 1);
    } catch (e) {
      handleError(e);
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  function reload() {
    if (draft && !window.confirm('Se descartarán los cambios sin guardar. ¿Recargar las direcciones?')) return;
    setDraft(null);
    setRevision((n) => n + 1);
  }
  return (
    <section aria-label="Mis direcciones" className="mt-7">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-semibold">Tus direcciones de entrega</h2>
        <button disabled={busy || loading} onClick={reload} className="text-sm underline disabled:opacity-40">
          Actualizar direcciones
        </button>
      </div>
      <p className="mt-3 text-sm text-stone-500">
        Guarda hasta 10 direcciones. Los cambios no modifican tu perfil ni los pedidos registrados.
      </p>
      {error && (
        <p role="alert" className="mt-4 rounded-xl bg-red-50 p-4 text-red-800">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="mt-4 rounded-xl bg-emerald-50 p-4">
          {notice}
        </p>
      )}
      {loading ? (
        <p role="status" className="py-6">
          Cargando direcciones…
        </p>
      ) : (
        <>
          {!draft && (
            <button
              disabled={busy || items.length >= 10 || !!error}
              onClick={() => {
                setFieldErrors({});
                setDraft({ ...empty, isDefault: items.length === 0 });
                setNotice('');
              }}
              className={`${button} my-5`}
            >
              Agregar dirección
            </button>
          )}
          {!items.length && !error && !draft && (
            <p className="rounded-xl border border-dashed border-stone-300 p-5 text-sm text-stone-500">
              Todavía no tienes direcciones guardadas. Agrega una dirección o completa los campos al comprar.
            </p>
          )}
          {!draft && (
            <ul className="space-y-4">
              {items.map((a) => (
                <li key={a.id} className="rounded-2xl border border-stone-200 bg-white p-5">
                  <div className="flex flex-wrap items-center gap-3">
                    <h3 className="break-words font-semibold">{a.label}</h3>
                    {a.isDefault && (
                      <span className="rounded-full bg-emerald-50 px-3 py-1 text-xs text-emerald-800">
                        Predeterminada
                      </span>
                    )}
                  </div>
                  <p className="mt-3 break-words">
                    {a.firstName} {a.lastName}
                  </p>
                  <p className="mt-1 break-words text-sm text-stone-600">{a.formattedAddress}</p>
                  {a.phone && <p className="mt-2 text-sm">{a.phone}</p>}
                  <div className="mt-4 flex flex-wrap gap-4 text-sm">
                    <button
                      disabled={busy}
                      aria-label={`Editar dirección ${a.label}`}
                      onClick={() => {
                        setFieldErrors({});
                        setDraft({ ...a, phone: a.phone || '' });
                        setNotice('');
                      }}
                      className="underline"
                    >
                      Editar
                    </button>
                    {!a.isDefault && (
                      <button
                        disabled={busy}
                        onClick={() =>
                          mutate('put', { ...a, isDefault: true }, 'Dirección predeterminada actualizada.')
                        }
                        className="underline"
                      >
                        Usar como predeterminada
                      </button>
                    )}
                    <button
                      disabled={busy}
                      aria-label={`Eliminar dirección ${a.label}`}
                      onClick={() => {
                        if (
                          window.confirm(`¿Eliminar la dirección “${a.label}”? Los pedidos existentes se conservarán.`)
                        )
                          mutate('delete', a, 'Dirección eliminada.');
                      }}
                      className="text-red-700 underline"
                    >
                      Eliminar
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
      {draft && (
        <form
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            if (!validateAddressForm(e.currentTarget, draft, setFieldErrors)) return;
            mutate(draft.id ? 'put' : 'post', draft, 'Dirección guardada.');
          }}
          className="mt-5 rounded-2xl border border-stone-200 bg-white p-5 sm:p-7"
        >
          <h3 className="mb-5 text-lg font-semibold">{draft.id ? 'Editar dirección' : 'Nueva dirección'}</h3>
          <fieldset disabled={busy || loading} className="space-y-5">
            <div className="grid gap-4 sm:grid-cols-2">
              {fields.map(([key, label, maxLength, required, autoComplete]) => (
                <label key={key} className="text-sm">
                  {label}
                  <input
                    type={key === 'phone' ? 'tel' : 'text'}
                    required={required}
                    maxLength={maxLength}
                    autoComplete={autoComplete}
                    minLength={key === 'postalCode' ? 3 : undefined}
                    value={draft[key]}
                    onChange={(e) => setDraft({ ...draft, [key]: e.target.value })}
                    className={input}
                  />
                </label>
              ))}
            </div>
            <DeliveryAddressFields key={draft.id || 'new'} value={draft} onChange={setDraft} errors={fieldErrors} />
            <label className="flex gap-3 text-sm">
              <input
                type="checkbox"
                disabled={!!lockedDefault || items.length === 0}
                checked={draft.isDefault}
                onChange={(e) => setDraft({ ...draft, isDefault: e.target.checked })}
              />
              Usar como dirección predeterminada
            </label>
            {lockedDefault && (
              <p className="text-xs text-stone-500">
                Para cambiar la predeterminada, selecciona otra dirección desde la lista.
              </p>
            )}
            <div className="flex flex-wrap gap-4">
              <button className={button}>{busy ? 'Guardando…' : 'Guardar dirección'}</button>
              <button type="button" onClick={() => setDraft(null)} className="text-sm underline">
                Cancelar
              </button>
            </div>
          </fieldset>
        </form>
      )}
    </section>
  );
}
