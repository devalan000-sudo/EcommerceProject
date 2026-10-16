import { colorHex, sizeLabel } from '../../utils/variants/variantOptions.js';

export default function VariantSelector({ variants, color, size, onColor, onSize }) {
  const active = variants.filter((v) => v.isActive);
  const colors = [...new Set(active.map((v) => v.color))];
  const sizes = [...new Set(active.map((v) => v.size))];
  return (
    <div className="mb-5 space-y-5">
      <fieldset>
        <legend className="mb-3 text-sm font-semibold">Color{color ? `: ${color}` : ' · Selecciona uno'}</legend>
        <div className="flex flex-wrap gap-2">
          {colors.map((c) => (
            <button
              key={c}
              type="button"
              aria-pressed={color === c}
              disabled={!active.some((v) => v.color === c && v.availableStock > 0)}
              onClick={() => onColor(c)}
              className={`flex items-center gap-2 rounded-xl border px-4 py-3 text-sm disabled:opacity-40 ${color === c ? 'border-brand bg-brand/10 ring-1 ring-brand' : 'border-stone-300'}`}
            >
              <span
                aria-hidden="true"
                className="h-5 w-5 rounded-full border border-stone-300"
                style={{ backgroundColor: colorHex(c) }}
              />
              {c}
            </button>
          ))}
        </div>
      </fieldset>
      <fieldset>
        <legend className="mb-3 text-sm font-semibold">Talla{!color ? ' · Primero elige un color' : ''}</legend>
        <div className="flex flex-wrap gap-2">
          {sizes.map((s) => (
            <button
              key={s}
              type="button"
              aria-pressed={size === s}
              disabled={!active.some((v) => v.color === color && v.size === s && v.availableStock > 0)}
              onClick={() => onSize(s)}
              className={`rounded-xl border px-4 py-3 text-sm disabled:opacity-40 ${size === s ? 'border-brand bg-brand text-white' : 'border-stone-300'}`}
            >
              {sizeLabel(s)}
            </button>
          ))}
        </div>
      </fieldset>
      {!active.some((v) => v.availableStock > 0) && (
        <p role="status" className="text-sm text-stone-500">
          Sin combinaciones disponibles. Consulta al negocio.
        </p>
      )}
      <p className="text-xs text-stone-500">
        Las opciones agotadas están deshabilitadas. Agregar al carrito no reserva existencias.
      </p>
    </div>
  );
}
