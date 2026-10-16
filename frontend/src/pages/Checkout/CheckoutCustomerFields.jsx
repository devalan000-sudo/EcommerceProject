const inputClass = 'mt-2 w-full rounded-xl border border-stone-300 bg-white p-3';
const fields = [
  ['firstName', 'Nombre', 'text', 100],
  ['lastName', 'Apellido', 'text', 100],
  ['email', 'Correo', 'email', 254],
  ['phone', 'Teléfono (opcional)', 'tel', 25],
];

export default function CheckoutCustomerFields({ value, onChange }) {
  return (
    <div className="my-5 grid gap-4 sm:grid-cols-2">
      {fields.map(([key, label, type, maxLength]) => (
        <label key={key} className="text-sm">
          {label}
          <input
            required={key !== 'phone'}
            type={type}
            maxLength={maxLength}
            value={value[key]}
            onChange={(event) => onChange({ ...value, [key]: event.target.value })}
            className={inputClass}
          />
        </label>
      ))}
    </div>
  );
}
