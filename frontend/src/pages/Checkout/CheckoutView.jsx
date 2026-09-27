import DeliveryAddressFields from '../../components/forms/DeliveryAddressFields.jsx';
import { emptyAddress, validateAddressForm, deliveryFromAddress } from '../../utils/addresses/addresses.js';
import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api/client.js';
import { errorMessage } from '../../services/api/errors.js';
import { useCart } from '../../contexts/CartContext/CartContext.jsx';
import { useCustomer } from '../../contexts/CustomerContext/CustomerContext.jsx';
import { useStore } from '../../contexts/StoreContext/StoreContext.jsx';
import { isDemoCatalog } from '../../data/catalogMode.js';
import { formatMXN } from '../../utils/money/money.js';
import { generateWhatsAppLink } from '../../utils/whatsapp/whatsapp.js';
import OrderResolutionNotice from '../../components/orders/OrderResolutionNotice.jsx';
import CheckoutHeader from '../../components/checkout/CheckoutHeader.jsx';
import CheckoutLoginPrompt from '../../components/checkout/CheckoutLoginPrompt.jsx';
import PaymentMethodOptions from './PaymentMethodOptions.jsx';
import CheckoutItemsList from './CheckoutItemsList.jsx';
import CheckoutTotals from './CheckoutTotals.jsx';
import CheckoutCustomerFields from './CheckoutCustomerFields.jsx';
import {
  authorizationHeaders,
  buildCheckoutPayload,
  checkoutLoginUrl,
  validateStripeCheckoutUrl,
} from './checkoutHelpers.js';
import {
  createOrder,
  getOrder,
  getPaymentStatus,
  loadCheckoutData,
  quoteOrder,
  startOrCancelPayment,
} from '../../services/checkout/checkoutService.js';
import { useOrderPaymentPolling } from '../../hooks/useOrderPaymentPolling/useOrderPaymentPolling.js';

const blank = { firstName: '', lastName: '', email: '', phone: '', address: '', shippingAddress: { ...emptyAddress } };
const inputClass = 'mt-2 w-full rounded-xl border border-stone-300 bg-white p-3';
const checkoutError = (e) =>
  e.response?.status === 403 ? 'Confirma tu correo desde Mi cuenta antes de finalizar la compra.' : errorMessage(e);

export default function CheckoutView() {
  const { businessName, settings, loading: storeLoading, error: storeError } = useStore();
  const { items, syncProducts } = useCart();
  const { session } = useCustomer();
  const [fieldErrors, setFieldErrors] = useState({});
  const [delivery, setDelivery] = useState(blank);
  const [addresses, setAddresses] = useState([]);
  const [addressId, setAddressId] = useState('');
  const [profileDelivery, setProfileDelivery] = useState(blank);
  const [method, setMethod] = useState('Card');
  const [status, setStatus] = useState('loading');
  const [error, setError] = useState('');
  const [quote, setQuote] = useState(null);
  const [order, setOrder] = useState(null);
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [stripeEnabled, setStripeEnabled] = useState(false);
  const lock = useRef(false);
  const payload = buildCheckoutPayload(delivery, method, items);
  const signature = JSON.stringify(payload);
  const headers = authorizationHeaders(session);
  useEffect(() => {
    getPaymentStatus(api)
      .then((r) => setStripeEnabled(r.data.enabled))
      .catch(() => setStripeEnabled(false));
  }, []);
  useOrderPaymentPolling(api, order, session, setOrder);
  async function payment(cancel = false) {
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    setError('');
    try {
      const { data } = await startOrCancelPayment(api, order.id, cancel, headers);
      if (!cancel && data.url) {
        const url = validateStripeCheckoutUrl(data.url);
        window.location.assign(url.href);
      } else {
        const result = await getOrder(api, order.id, headers);
        setOrder(result.data);
      }
    } catch (e) {
      setError(checkoutError(e));
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  useEffect(() => {
    if (!session || isDemoCatalog) return;
    const controller = new AbortController();
    const config = { headers: { Authorization: `Bearer ${session.token}` }, signal: controller.signal };
    const orderId = new URLSearchParams(location.search).get('order');
    setStatus('loading');
    setQuote(null);
    setOrder(null);
    if (orderId) {
      getOrder(api, orderId, config.headers)
        .then((r) => {
          setOrder(r.data);
          setStatus('ready');
        })
        .catch((e) => {
          if (!controller.signal.aborted) {
            setError(checkoutError(e));
            setStatus('error');
          }
        });
    } else {
      loadCheckoutData(api, session, controller.signal)
        .then(([profile, products, savedAddresses]) => {
          if (controller.signal.aborted) return;
          const { firstName, lastName, email, phone, address } = profile.data;
          const original = {
            firstName,
            lastName,
            email,
            phone: phone || '',
            address: '',
            shippingAddress: { ...emptyAddress },
          };
          const preferred = savedAddresses.data.find((a) => a.isDefault);
          setProfileDelivery(original);
          setAddresses(savedAddresses.data);
          setAddressId(preferred ? String(preferred.id) : '');
          setDelivery(preferred ? deliveryFromAddress(preferred, email) : original);
          syncProducts(products.data);
          setStatus('ready');
        })
        .catch((e) => {
          if (!controller.signal.aborted) {
            setError(checkoutError(e));
            setStatus('error');
          }
        });
    }
    return () => controller.abort();
  }, [session, syncProducts]);

  async function review(event) {
    event.preventDefault();
    if (!validateAddressForm(event.currentTarget, delivery.shippingAddress, setFieldErrors)) return;
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    setError('');
    setQuote(null);
    try {
      const { data } = await quoteOrder(api, payload, headers);
      setQuote({ data, signature });
    } catch (e) {
      setError(checkoutError(e));
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  async function save() {
    if (lock.current || quote?.signature !== signature) return;
    lock.current = true;
    setBusy(true);
    setError('');
    try {
      const stored = JSON.parse(sessionStorage.getItem('checkout-request') || 'null');
      const request = stored?.signature === signature ? stored : { signature, id: crypto.randomUUID() };
      sessionStorage.setItem('checkout-request', JSON.stringify(request));
      const { data } = await createOrder(api, payload, request.id, headers);
      setUncertain(false);
      sessionStorage.removeItem('checkout-request');
      setOrder(data);
      window.history.replaceState(null, '', `/checkout?order=${encodeURIComponent(data.id)}`);
    } catch (e) {
      if (!e.response || e.response.status >= 500) setUncertain(true);
      setError(checkoutError(e));
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  function whatsapp() {
    try {
      const url = new URL(
        generateWhatsAppLink(
          order.items.map((i) => ({
            name: i.productName,
            price: i.unitPrice,
            quantity: i.quantity,
            details: i.details,
            size: i.size,
            color: i.color,
          })),
          order.subtotal,
          settings.whatsAppPhone,
        ),
      );
      url.searchParams.set(
        'text',
        `Pedido ${order.number}\n${url.searchParams.get('text')}\nEntrega: ${order.shippingName || 'Gestionado por la tienda'}${order.total != null ? `\nEnvío: ${formatMXN(order.shippingCost)}\nTotal con entrega: ${formatMXN(order.total)} MXN` : ''}`,
      );
      window.open(url.href, '_blank', 'noopener,noreferrer');
    } catch (e) {
      setError(e.message);
    }
  }
  const summary = order || (quote?.signature === signature ? quote.data : null);
  const requestedOrder = new URLSearchParams(location.search).get('order');
  const loginUrl = checkoutLoginUrl(requestedOrder);
  return (
    <>
      <CheckoutHeader businessName={businessName} />
      <main className="mx-auto max-w-5xl px-5 py-10 text-brand">
        <a href="/comprar" className="text-sm underline">
          ← Seguir comprando
        </a>
        <h1 className="my-6 text-3xl font-semibold">{order ? 'Tu pedido está registrado' : 'Finalizar compra'}</h1>
        {isDemoCatalog ? (
          <p>El checkout requiere el catálogo real. La simulación de WhatsApp sigue disponible en el carrito.</p>
        ) : !session ? (
          <CheckoutLoginPrompt loginUrl={loginUrl} />
        ) : (
          <>
            {error && (
              <p role="alert" className="mb-5 rounded-xl bg-red-50 p-4 text-red-700">
                {error}{' '}
                <a
                  href={/talla|color|variante|producto|unidades/i.test(error) ? '/comprar' : '/account'}
                  className="underline"
                >
                  {/talla|color|variante|producto|unidades/i.test(error)
                    ? 'Volver a elegir productos'
                    : 'Ir a Mi cuenta'}
                </a>
              </p>
            )}
            {status === 'loading' && <p role="status">Preparando tu compra…</p>}
            {status === 'error' && (
              <button onClick={() => location.reload()} className="underline">
                Reintentar carga
              </button>
            )}
            {status === 'ready' && !order && !items.length && (
              <p>Tu carrito está vacío o sus productos ya no están disponibles.</p>
            )}
            {status === 'ready' && !order && items.length > 0 && (
              <form noValidate onSubmit={review} className="rounded-2xl border border-stone-200 bg-white p-6">
                <fieldset disabled={busy || uncertain}>
                  <h2 className="text-xl font-semibold">Tu selección</h2>
                  <CheckoutItemsList items={items} />
                  <h2 className="text-xl font-semibold">Datos de entrega</h2>
                  <p className="mt-2 text-sm text-stone-500">
                    Estos datos se guardan en el pedido; no cambian tu perfil.
                  </p>
                  <label className="my-5 block text-sm">
                    Dirección guardada
                    <select
                      value={addressId}
                      onChange={(e) => {
                        const id = e.target.value;
                        setAddressId(id);
                        setFieldErrors({});
                        const selected = addresses.find((a) => String(a.id) === id);
                        setDelivery(
                          selected
                            ? deliveryFromAddress(selected, delivery.email)
                            : { ...profileDelivery, email: delivery.email },
                        );
                      }}
                      className={inputClass}
                    >
                      <option value="">Capturar una nueva dirección</option>
                      {addresses.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.label}
                          {a.isDefault ? ' · Predeterminada' : ''} · {a.city}
                        </option>
                      ))}
                    </select>
                  </label>
                  <p className="mb-4 text-sm text-stone-500">
                    Puedes ajustar los datos de abajo solo para este pedido.{' '}
                    <a href="/account?tab=addresses&returnTo=checkout" className="underline">
                      Administrar mis direcciones
                    </a>
                  </p>
                  <CheckoutCustomerFields value={delivery} onChange={setDelivery} />
                  <DeliveryAddressFields
                    key={addressId}
                    value={delivery.shippingAddress}
                    onChange={(shippingAddress) => setDelivery({ ...delivery, shippingAddress })}
                    errors={fieldErrors}
                  />
                  <p className="my-5 text-sm text-stone-500">
                    La tienda gestionará el envío a la dirección indicada. El pago en línea incluye los productos; no se
                    añade un cargo de envío en este checkout.
                  </p>
                  <PaymentMethodOptions
                    method={method}
                    onMethodChange={setMethod}
                    stripeEnabled={stripeEnabled}
                    storeLoading={storeLoading}
                    storeError={storeError}
                    whatsAppPhone={settings.whatsAppPhone}
                  />
                  <button className="rounded-xl bg-brand px-6 py-3 font-semibold text-white disabled:opacity-40">
                    {busy ? 'Consultando…' : 'Revisar pedido e importe'}
                  </button>
                </fieldset>
              </form>
            )}
            {order && (
              <a
                href={`/account?tab=orders&order=${encodeURIComponent(order.id)}`}
                className="mt-5 inline-block text-sm underline"
              >
                Ver seguimiento en Mis pedidos →
              </a>
            )}
            {summary && (
              <section className="mt-6 rounded-2xl border border-stone-200 bg-white p-6">
                <h2 className="break-all text-xl font-semibold">
                  {order ? order.number : 'Resumen calculado por la tienda'}
                </h2>
                <p className="mt-3 break-words text-sm text-stone-500">
                  {summary.firstName} {summary.lastName} · {summary.email}
                </p>
                <p className="break-words text-sm text-stone-500">{summary.address}</p>
                <ul className="my-4 divide-y divide-stone-200">
                  {summary.items.map((i, index) => (
                    <li key={index} className="flex justify-between gap-4 py-3">
                      <div className="min-w-0 break-words">
                        {i.quantity} × {i.productName}
                        <p className="text-sm text-stone-500">
                          {[i.size, i.color].filter(Boolean).join(' · ')} {i.details}
                        </p>
                      </div>
                      <span className="shrink-0">{formatMXN(i.lineTotal)}</span>
                    </li>
                  ))}
                </ul>
                <CheckoutTotals summary={summary} />
                {!order && (
                  <>
                    <p className="my-4 text-sm text-stone-500">
                      Revisa el importe. Los precios y la disponibilidad se vuelven a validar al guardar; no se
                      realizará ningún cobro.
                    </p>
                    <button
                      disabled={busy}
                      onClick={save}
                      className="rounded-xl bg-brand px-6 py-3 text-white disabled:opacity-40"
                    >
                      {busy ? 'Guardando…' : uncertain ? 'Reintentar confirmación' : 'Confirmar y guardar pedido'}
                    </button>
                  </>
                )}
                {order && (
                  <>
                    <OrderResolutionNotice order={order} />
                    <p className="my-4" role="status">
                      {order.paymentStatus === 'Paid'
                        ? order.paymentMethod === 'Card'
                          ? 'Pago de prueba confirmado. ¡Gracias por tu compra!'
                          : 'Pago confirmado por el negocio. ¡Gracias por tu compra!'
                        : order.paymentStatus === 'Refunded'
                          ? 'Pago reembolsado.'
                          : order.paymentStatus === 'Failed'
                            ? 'Pedido cancelado o pago vencido. Puedes crear un nuevo pedido.'
                            : 'Pendiente de pago o confirmación.'}
                    </p>
                    {order.paymentMethod === 'WhatsApp' ? (
                      <button
                        disabled={storeLoading || storeError || !settings.whatsAppPhone}
                        onClick={whatsapp}
                        className="text-sm underline disabled:opacity-40"
                      >
                        Abrir WhatsApp con mi pedido
                      </button>
                    ) : (
                      order.paymentStatus === 'Pending' &&
                      !order.cancellationPending &&
                      !order.refundStatus && (
                        <>
                          <button
                            disabled={busy || !stripeEnabled}
                            onClick={() => payment()}
                            className="rounded-xl bg-brand px-6 py-3 font-semibold text-white disabled:opacity-40"
                          >
                            {busy ? 'Consultando Stripe…' : 'Pagar con tarjeta · Prueba'}
                          </button>
                          {!stripeEnabled && (
                            <p className="mt-3 text-sm text-stone-500">
                              El negocio debe configurar Stripe de prueba para habilitar el pago.
                            </p>
                          )}
                          <div className="mt-4 flex flex-wrap gap-5 text-sm">
                            <button disabled={busy} onClick={() => payment(true)} className="underline">
                              Cancelar sesión de pago
                            </button>
                            <button onClick={() => location.reload()} className="underline">
                              Actualizar estado
                            </button>
                          </div>
                          <p className="mt-3 text-sm text-stone-500">
                            Conserva este enlace. El pago se confirma al recibir la respuesta de Stripe; regresar a esta
                            página no confirma un cobro.
                          </p>
                        </>
                      )
                    )}
                  </>
                )}
              </section>
            )}
            {uncertain && (
              <p role="alert" className="mt-5 rounded-xl bg-amber-50 p-4">
                No pudimos confirmar si se guardó el pedido. Reintenta con el botón de confirmación: reutilizaremos el
                mismo identificador para evitar duplicados.
              </p>
            )}
          </>
        )}
      </main>
    </>
  );
}
