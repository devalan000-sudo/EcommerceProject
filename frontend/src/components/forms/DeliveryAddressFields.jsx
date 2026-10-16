import { useId, useState } from 'react';
import { addressFields, validateAddress } from '../../utils/addresses/addresses.js';

export default function DeliveryAddressFields({ value, onChange, errors = {} }) {
  const id = useId();
  const [touched, setTouched] = useState({});
  const currentErrors = validateAddress(value);
  return (
    <fieldset className="my-5 space-y-4">
      <legend className="font-semibold">Dirección de entrega</legend>
      <p className="text-sm text-stone-500">Completa los campos obligatorios. Los opcionales están indicados.</p>
      <div className="grid gap-4 sm:grid-cols-2">
        {addressFields.map(([key, label, max, autoComplete, optional]) => {
          const error = touched[key] ? currentErrors[key] : errors[key];
          return (
            <div key={key}>
              <label htmlFor={`${id}-${key}`} className="text-sm">
                {label}
              </label>
              <input
                id={`${id}-${key}`}
                name={key}
                data-address-field={key}
                required={!optional}
                maxLength={max}
                autoComplete={autoComplete}
                value={value[key] || ''}
                type="text"
                aria-invalid={!!error}
                aria-describedby={error ? `${id}-${key}-error` : undefined}
                onBlur={() => setTouched((previous) => ({ ...previous, [key]: true }))}
                onChange={(e) => onChange({ ...value, [key]: e.target.value })}
                className={`mt-2 w-full rounded-xl border bg-white p-3 ${error ? 'border-red-600' : 'border-stone-300'}`}
              />
              {key === 'exteriorNumber' && (
                <label className="mt-2 flex gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={value.exteriorNumber === 'S/N'}
                    onChange={(e) => onChange({ ...value, exteriorNumber: e.target.checked ? 'S/N' : '' })}
                  />
                  Sin número
                </label>
              )}
              {error && (
                <p id={`${id}-${key}-error`} className="mt-1 text-sm text-red-700">
                  {error}
                </p>
              )}
            </div>
          );
        })}
      </div>
      <label className="block text-sm">
        Referencias de entrega (opcional)
        <textarea
          maxLength={150}
          rows={3}
          value={value.references || ''}
          onChange={(e) => onChange({ ...value, references: e.target.value })}
          className="mt-2 w-full rounded-xl border border-stone-300 bg-white p-3"
        />
      </label>
    </fieldset>
  );
}
