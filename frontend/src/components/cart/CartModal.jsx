import { useStore } from '../../contexts/StoreContext/StoreContext.jsx';
import { useEffect, useRef, useState } from 'react';
import { useCart } from '../../contexts/CartContext/CartContext.jsx';
import { cartItemKey, quantityLimit } from '../../utils/cart/cart.js';
import { formatMXN, toCents } from '../../utils/money/money.js';
import { isDemoCatalog } from '../../data/catalogMode.js';
import { cartWhatsAppLink, cartWhatsAppMessage } from '../../services/cart/cartService.js';
import CartProductImage from './CartProductImage.jsx';

export default function CartModal({ open, onClose, catalogReady }) {
  const { settings, loading: storeLoading, error: storeError } = useStore();
  const dialog = useRef(null);
  const { items, total, setQuantity, remove, setDetails } = useCart();
  const [testPhone, setTestPhone] = useState('');
  let message = '';
  try {
    message = cartWhatsAppMessage(items, total, isDemoCatalog);
  } catch {
    /* El envío valida los datos y muestra el error. */
  }
  const [error, setError] = useState('');
  useEffect(() => {
    const element = dialog.current;
    if (open) {
      element.showModal();
      setError('');
    } else element.close();
  }, [open]);
  useEffect(() => {
    if (!open) return;
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = previous;
    };
  }, [open]);

  function checkout() {
    try {
      const link = cartWhatsAppLink(items, total, isDemoCatalog ? testPhone : settings.whatsAppPhone, isDemoCatalog);
      window.open(link, '_blank', 'noopener,noreferrer');
      setError('');
    } catch (e) {
      setError(e.message);
    }
  }

  return (
    <dialog
      ref={dialog}
      onCancel={onClose}
      onClose={onClose}
      aria-labelledby="cart-title"
      className="m-auto max-h-[90dvh] w-[calc(100%-2rem)] max-w-lg overflow-y-auto rounded-2xl bg-white p-5 text-brand sm:p-7"
    >
      <div className="flex items-center justify-between gap-4">
        <h2 id="cart-title" className="text-2xl font-semibold">
          Tu carrito
        </h2>
        <button autoFocus onClick={onClose} className="rounded-lg p-3" aria-label="Cerrar carrito">
          ✕
        </button>
      </div>
      {items.length === 0 ? (
        <p className="py-12 text-stone-600">Tu carrito está vacío. Encuentra algo que te guste.</p>
      ) : (
        <ul className="my-5 divide-y divide-stone-200">
          {items.map((item) => (
            <li key={cartItemKey(item)} className="py-4">
              <div className="flex items-center gap-3">
                <CartProductImage item={item} />
                <div className="flex min-w-0 flex-1 flex-col gap-1 sm:flex-row sm:items-start sm:justify-between sm:gap-3">
                  <h3 className="min-w-0 break-words font-semibold">{item.name}</h3>
                  {item.variantId && (
                    <p className="text-xs text-stone-500">
                      {item.size} · {item.color}
                    </p>
                  )}
                  <span className="shrink-0">{formatMXN((toCents(item.price) * item.quantity) / 100)}</span>
                </div>
              </div>
              <div className="mt-3 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <button
                    aria-label={`Reducir cantidad de ${item.name}`}
                    disabled={item.quantity === 1}
                    onClick={() => setQuantity(cartItemKey(item), item.quantity - 1)}
                    className="h-11 w-11 rounded-lg border border-stone-200 disabled:opacity-30"
                  >
                    −
                  </button>
                  <span>{item.quantity}</span>
                  <button
                    aria-label={`Aumentar cantidad de ${item.name}`}
                    disabled={item.quantity >= quantityLimit(item)}
                    onClick={() => setQuantity(cartItemKey(item), item.quantity + 1)}
                    className="h-11 w-11 rounded-lg border border-stone-200 disabled:opacity-30"
                  >
                    +
                  </button>
                </div>
                <button onClick={() => remove(cartItemKey(item))} className="p-2 text-sm underline">
                  Eliminar
                </button>
              </div>
              <label className="mt-3 block text-xs text-stone-600">
                {item.variantId ? 'Notas del artículo (opcional)' : 'Colores, tallas o detalles (opcional)'}
                <input
                  value={typeof item.details === 'string' ? item.details : ''}
                  onChange={(event) => setDetails(cartItemKey(item), event.target.value)}
                  maxLength={200}
                  placeholder={
                    item.variantId ? 'Ej.: envolver para regalo' : 'Ej.: una color rojo y otra negra, talla M'
                  }
                  className="mt-1 w-full rounded-lg border border-stone-200 p-3 text-sm"
                />
              </label>
              <p className="mt-1 text-[11px] text-stone-500">
                {item.variantId
                  ? 'Estas notas no cambian la talla ni el color seleccionados.'
                  : 'Preferencias sujetas a disponibilidad. Si cambias la cantidad, revisa estos detalles.'}
              </p>
            </li>
          ))}
        </ul>
      )}
      <div className="flex justify-between border-t border-stone-200 pt-5 text-xl font-semibold">
        <span>Total</span>
        <span>{formatMXN(total)} MXN</span>
      </div>
      <p className="my-3 text-sm text-stone-500">
        {isDemoCatalog
          ? 'Confirma disponibilidad, entrega y pago con el negocio.'
          : 'El envío se calcula al elegir la entrega en el checkout.'}{' '}
        {!catalogReady && 'Esperando la actualización del catálogo para continuar.'}
      </p>
      {!isDemoCatalog && items.length > 0 && catalogReady && (
        <a
          href="/checkout"
          className="my-4 block rounded-xl bg-brand p-4 text-center font-semibold text-white hover:bg-emerald-900"
        >
          Continuar con mi compra
        </a>
      )}
      {message && (
        <details className="my-4 rounded-xl bg-stone-50 p-4">
          <summary className="cursor-pointer text-sm font-semibold">Mensaje para coordinar por WhatsApp</summary>
          <p className="mt-2 whitespace-pre-wrap break-words text-sm text-stone-600">{message}</p>
        </details>
      )}
      {isDemoCatalog && (
        <label className="my-4 block text-sm">
          Tu WhatsApp para la simulación
          <input
            type="tel"
            value={testPhone}
            onChange={(event) => setTestPhone(event.target.value)}
            placeholder="Código de país + número"
            className="mt-2 w-full rounded-lg border border-stone-200 p-3"
          />
          <span className="mt-1 block text-xs text-stone-500">
            Incluye 52 para México. Es una prueba, no una compra real.
          </span>
        </label>
      )}
      {error && (
        <p role="alert" className="my-3 text-sm text-red-700">
          {error}
        </p>
      )}
      <button
        disabled={
          !items.length || !catalogReady || (!isDemoCatalog && (storeLoading || storeError || !settings.whatsAppPhone))
        }
        onClick={checkout}
        className="w-full p-3 text-sm text-stone-500 underline disabled:cursor-not-allowed disabled:opacity-40"
      >
        Prefiero pedir por WhatsApp ↗
      </button>
      <p className="mt-2 text-center text-xs text-stone-500">
        {!isDemoCatalog && (storeError || !settings.whatsAppPhone)
          ? 'El contacto por WhatsApp no está disponible por ahora.'
          : 'Se abrirá WhatsApp con el texto preparado. Tú confirmas el envío.'}
      </p>
    </dialog>
  );
}
