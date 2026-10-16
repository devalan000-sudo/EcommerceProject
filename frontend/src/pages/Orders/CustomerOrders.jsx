import ReturnRequests from './ReturnRequests.jsx';
import { useStore } from '../../contexts/StoreContext/StoreContext.jsx';
import { useEffect, useState } from 'react';
import { api } from '../../services/api/client.js';
import { getCustomerOrders } from '../../services/orders/ordersService.js';
import { errorMessage } from '../../services/api/errors.js';
import { formatMXN } from '../../utils/money/money.js';
import OrderResolutionNotice from '../../components/orders/OrderResolutionNotice.jsx';
import {
  canResumePayment,
  deliveryLabels,
  formatOrderDate,
  isOrderId,
  orderCheckoutUrl,
  orderContactLink,
  paymentLabels,
  paymentMethodLabels,
} from '../../utils/orders/orders.js';

const primary = 'inline-block rounded-xl bg-brand px-5 py-3 text-sm font-semibold text-white';
const pageSize = 20;

function Badges({ order }) {
  return (
    <div className="my-4 flex flex-wrap gap-2">
      <span className="rounded-full bg-stone-100 px-3 py-2 text-xs">
        Pago: {paymentLabels[order.paymentStatus] || 'Por confirmar'}
      </span>
      <span className="rounded-full bg-brand/5 px-3 py-2 text-xs">
        Entrega: {deliveryLabels[order.fulfillmentStatus] || 'Por confirmar'}
      </span>
    </div>
  );
}

function ProductImage({ src }) {
  const [failed, setFailed] = useState(false);
  return (
    <div className="flex size-16 shrink-0 items-center justify-center rounded-lg border border-stone-100 bg-white">
      {src && !failed ? (
        <img src={src} alt="" loading="lazy" onError={() => setFailed(true)} className="size-full object-contain p-1" />
      ) : (
        <span className="text-center text-xs text-stone-400">Sin imagen</span>
      )}
    </div>
  );
}

export default function CustomerOrders({ session, onExpired }) {
  const { settings, loading: storeLoading, error: storeError } = useStore();
  const params = new URLSearchParams(location.search);
  const id = params.get('order');
  const initialPage = Number(params.get('page'));
  const [page, setPage] = useState(
    Number.isInteger(initialPage) && initialPage >= 1 && initialPage <= 100000 ? initialPage : 1,
  );
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [contactError, setContactError] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setData(null);
    setError('');
    setContactError('');
    if (id !== null && !isOrderId(id)) {
      setError('El enlace del pedido no es válido.');
      setLoading(false);
      return () => controller.abort();
    }
    setLoading(true);
    getCustomerOrders(api, session, page, id, controller.signal)
      .then((r) => {
        if (!controller.signal.aborted) setData(r.data);
      })
      .catch((e) => {
        if (controller.signal.aborted) return;
        if (e.response?.status === 401 || e.response?.status === 403) {
          onExpired();
          return;
        }
        setError(e.response?.status === 404 ? 'No encontramos este pedido en tu cuenta.' : errorMessage(e));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [session.token, id, page, revision, onExpired]);

  function changePage(next) {
    setPage(next);
    window.history.replaceState(null, '', `/account?tab=orders&page=${next}`);
  }
  function contact() {
    try {
      window.open(orderContactLink(data, settings.whatsAppPhone), '_blank', 'noopener,noreferrer');
    } catch (e) {
      setContactError(e.message);
    }
  }
  return (
    <section aria-label="Mis pedidos" className="mt-7">
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-semibold">{id ? 'Detalle de tu pedido' : 'Tus compras'}</h2>
        <div className="flex gap-4">
          {id && (
            <a href={`/account?tab=orders&page=${page}`} className="text-sm underline">
              Volver a mis pedidos
            </a>
          )}
          <button
            disabled={loading}
            onClick={() => setRevision((n) => n + 1)}
            className="text-sm underline disabled:opacity-40"
          >
            Actualizar
          </button>
        </div>
      </div>
      {loading && (
        <p role="status" className="py-6">
          Consultando tus pedidos…
        </p>
      )}
      {error && (
        <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-800">
          {error}
        </p>
      )}
      {!loading &&
        !error &&
        data &&
        (id ? (
          <article className="rounded-2xl border border-stone-200 bg-white p-5 sm:p-7">
            <h3 className="break-all font-semibold">{data.number}</h3>
            <p className="mt-2 text-sm text-stone-500">{formatOrderDate(data.createdAtUtc)} · Hora local</p>
            <Badges order={data} />
            <p className="text-sm">Forma de pago: {paymentMethodLabels[data.paymentMethod]}</p>
            <OrderResolutionNotice order={data} />
            {data.paymentMethod === 'WhatsApp' && data.paymentStatus === 'Pending' && (
              <p className="mt-3 text-sm text-stone-500">
                El pago se acuerda con el negocio por WhatsApp. Este estado no confirma un cobro.
              </p>
            )}
            <ul className="my-5 divide-y divide-stone-200">
              {data.items.map((item, index) => (
                <li key={index} className="flex flex-wrap gap-3 py-4">
                  <ProductImage src={item.imageUrl} />
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">
                      {item.quantity} × {item.productName}
                    </p>
                    <p className="text-sm text-stone-500">
                      {[item.size, item.color, item.details].filter(Boolean).join(' · ')}
                    </p>
                    <p className="text-sm">{formatMXN(item.unitPrice)} c/u</p>
                  </div>
                  <p className="text-sm font-semibold">{formatMXN(item.lineTotal)}</p>
                </li>
              ))}
            </ul>
            <div className="space-y-1 border-t border-stone-200 pt-4 text-right">
              <p>Productos: {formatMXN(data.subtotal)}</p>
              <p>Entrega: {data.shippingCost == null ? 'Por acordar' : formatMXN(data.shippingCost)}</p>
              <p className="pt-2 text-lg font-semibold">
                Total: {data.total == null ? 'Pendiente de cotizar' : `${formatMXN(data.total)} MXN`}
              </p>
            </div>
            <div className="mt-6 rounded-xl bg-stone-50 p-4">
              <h3 className="font-semibold">Entrega y seguimiento</h3>
              <p className="mt-2">{data.shippingName || 'Entrega por acordar'}</p>
              <p className="mt-2">
                {data.firstName} {data.lastName}
              </p>
              <p className="whitespace-pre-wrap break-words text-sm">{data.address}</p>
              <p className="mt-2 break-all text-sm">{data.email}</p>
              {data.phone && <p className="text-sm">{data.phone}</p>}
              {data.carrier && data.trackingNumber ? (
                <div className="mt-4 border-t border-stone-200 pt-3">
                  <p>Paquetería: {data.carrier}</p>
                  <p className="break-all">
                    Guía: <span className="select-all font-semibold">{data.trackingNumber}</span>
                  </p>
                  <p className="mt-1 text-xs text-stone-500">
                    Consulta este número con la paquetería. El seguimiento aquí se actualiza por el negocio.
                  </p>
                </div>
              ) : (
                <p className="mt-4 text-sm text-stone-500">
                  No hay guía registrada. La entrega local o recogida puede realizarse sin guía.
                </p>
              )}
            </div>
            {canResumePayment(data) && (
              <div className="mt-5">
                <a href={orderCheckoutUrl(data.id)} className={primary}>
                  Retomar pago
                </a>
                <p className="mt-2 text-xs text-stone-500">
                  Volverás al mismo pedido. La disponibilidad y la sesión de pago se comprobarán antes de continuar.
                </p>
              </div>
            )}
            {data.paymentStatus === 'Failed' && (
              <p className="mt-4 text-sm">
                Este pago falló o venció. Puedes{' '}
                <a href="/comprar" className="underline">
                  volver al catálogo
                </a>{' '}
                para preparar un nuevo pedido.
              </p>
            )}
            <button
              disabled={storeLoading || storeError || !settings.whatsAppPhone}
              onClick={contact}
              className="mt-5 text-sm text-stone-600 underline disabled:opacity-40"
            >
              Consultar este pedido por WhatsApp
            </button>
            {contactError && (
              <p role="alert" className="mt-3 text-sm text-red-700">
                {contactError}
              </p>
            )}
            <ReturnRequests key={data.id} session={session} orderId={data.id} onExpired={onExpired} />
          </article>
        ) : (
          <>
            {data.length === 0 ? (
              <div className="rounded-2xl border border-stone-200 bg-white p-7">
                <p>{page === 1 ? 'Aún no tienes pedidos registrados.' : 'No hay más pedidos en esta página.'}</p>
                <p className="mt-2 text-sm text-stone-500">
                  Aquí aparecerán las compras que guardes desde el checkout de tu cuenta.
                </p>
                <a href="/comprar" className={`${primary} mt-5`}>
                  Explorar productos
                </a>
              </div>
            ) : (
              <ul className="space-y-4">
                {data.map((order) => (
                  <li key={order.id} className="rounded-2xl border border-stone-200 bg-white p-5">
                    <p className="break-all font-semibold">{order.number}</p>
                    <p className="mt-2 text-sm text-stone-500">{formatOrderDate(order.createdAtUtc)}</p>
                    <Badges order={order} />
                    <p className="text-sm">{paymentMethodLabels[order.paymentMethod]}</p>
                    <p className="mt-2 font-semibold">
                      {order.total == null ? 'Total por acordar' : `${formatMXN(order.total)} MXN`}
                    </p>
                    <div className="mt-4 flex flex-wrap items-center gap-4">
                      <a
                        href={`/account?tab=orders&page=${page}&order=${encodeURIComponent(order.id)}`}
                        className="text-sm font-semibold underline"
                      >
                        Ver detalle
                      </a>
                      {canResumePayment(order) && (
                        <a href={orderCheckoutUrl(order.id)} className={primary}>
                          Retomar pago
                        </a>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
            )}
            <nav aria-label="Páginas de pedidos" className="mt-5 flex items-center justify-between gap-3">
              <button
                disabled={page <= 1}
                onClick={() => changePage(page - 1)}
                className="rounded-lg border px-4 py-2 disabled:opacity-40"
              >
                Anterior
              </button>
              <span className="text-sm">Página {page}</span>
              <button
                disabled={data.length < pageSize || page >= 100000}
                onClick={() => changePage(page + 1)}
                className="rounded-lg border px-4 py-2 disabled:opacity-40"
              >
                Siguiente
              </button>
            </nav>
          </>
        ))}
    </section>
  );
}
