import { useEffect, useState } from 'react';
import { api } from '../../services/api/client.js';
import { getAdminDashboard } from '../../services/admin/adminService.js';
import { errorMessage } from '../../services/api/errors.js';
import { formatMXN } from '../../utils/money/money.js';
import { formatOrderDate, paymentLabels, deliveryLabels, paymentMethodLabels } from '../../utils/orders/orders.js';
import AdminMetric from './AdminMetric.jsx';
const Metric = AdminMetric;

function initialFilters() {
  const now = new Date();
  const start = new Date(now);
  start.setUTCDate(start.getUTCDate() - 29);
  return { from: start.toISOString().slice(0, 10), to: now.toISOString().slice(0, 10), stockThreshold: 5 };
}
const input = 'mt-2 w-full rounded-xl border border-stone-300 bg-white p-3';
export default function AdminDashboard({ session, onOrders, onInventory }) {
  const [draft, setDraft] = useState(initialFilters);
  const [filters, setFilters] = useState(initialFilters);
  const [page, setPage] = useState(1);
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    setData(null);
    const query = new URLSearchParams({ ...filters, stockPage: page });
    getAdminDashboard(api, session, query, controller.signal)
      .then((r) => {
        if (!controller.signal.aborted) setData(r.data);
      })
      .catch((e) => {
        if (!controller.signal.aborted) setError(errorMessage(e));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [session.token, filters, page, revision]);
  function apply(event) {
    event.preventDefault();
    const days = (Date.parse(`${draft.to}T00:00:00Z`) - Date.parse(`${draft.from}T00:00:00Z`)) / 86400000;
    if (!Number.isFinite(days) || days < 0 || days > 365) {
      setError('Selecciona un período de hasta 366 días, con fecha inicial anterior o igual a la final.');
      return;
    }
    setPage(1);
    setFilters({ ...draft, stockThreshold: Number(draft.stockThreshold) });
  }
  return (
    <section aria-label="Resumen administrativo" className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold">Tu negocio de un vistazo</h2>
          <p className="mt-2 text-sm text-stone-500">
            Ventas registradas, trabajo pendiente y disponibilidad del catálogo.
          </p>
        </div>
        <button
          disabled={loading}
          onClick={() => setRevision((n) => n + 1)}
          className="text-sm underline disabled:opacity-40"
        >
          Actualizar resumen
        </button>
      </div>
      <form onSubmit={apply} className="rounded-2xl border border-stone-200 bg-white p-5">
        <fieldset disabled={loading}>
          <div className="grid gap-4 sm:grid-cols-3">
            <label className="text-sm">
              Desde (UTC)
              <input
                required
                type="date"
                max={draft.to || '9999-12-30'}
                value={draft.from}
                onChange={(e) => setDraft({ ...draft, from: e.target.value })}
                className={input}
              />
            </label>
            <label className="text-sm">
              Hasta (UTC, incluido)
              <input
                required
                type="date"
                min={draft.from}
                max="9999-12-30"
                value={draft.to}
                onChange={(e) => setDraft({ ...draft, to: e.target.value })}
                className={input}
              />
            </label>
            <label className="text-sm">
              Avisar con existencias disponibles de
              <input
                required
                type="number"
                min="0"
                max="1000000"
                step="1"
                value={draft.stockThreshold}
                onChange={(e) => setDraft({ ...draft, stockThreshold: e.target.value })}
                className={input}
              />
              <span className="mt-1 block text-xs text-stone-500">Este número o menos, por talla y color.</span>
            </label>
          </div>
          <div className="mt-4 flex flex-wrap gap-4">
            <button className="rounded-xl bg-brand px-5 py-3 text-sm font-semibold text-white disabled:opacity-40">
              Aplicar filtros
            </button>
            <button
              type="button"
              className="text-sm underline"
              onClick={() => {
                const reset = initialFilters();
                setDraft(reset);
                setFilters(reset);
                setPage(1);
              }}
            >
              Últimos 30 días
            </button>
          </div>
        </fieldset>
      </form>
      {error && (
        <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-800">
          {error}
        </p>
      )}
      {loading && <p role="status">Calculando el resumen…</p>}
      {data && (
        <>
          <section aria-label="Ventas del período">
            <h3 className="mb-3 font-semibold">
              Pedidos creados del {data.from} al {data.to} · UTC
            </h3>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <Metric
                label="Pedidos creados"
                value={data.sales.ordersCreated}
                help="Incluye todos los estados de pago y entrega."
              />
              <Metric
                label="Pedidos pagados"
                value={data.sales.paidOrders}
                help="Pagados actualmente, con total conocido en MXN."
              />
              <Metric
                label="Ventas confirmadas vigentes"
                value={formatMXN(data.sales.paidTotal)}
                help="MXN, incluye entrega y descuenta reembolsos parciales confirmados."
              />
              <Metric
                label="Importe reembolsado"
                value={formatMXN(data.sales.refundedTotal)}
                help={`${data.sales.refundedOrders} pedidos con reembolso total o parcial · MXN.`}
              />
            </div>
            <p className="mt-3 text-xs leading-relaxed text-stone-500">
              Se agrupan por creación del pedido y se consulta su estado actual, no la fecha del cobro o del reembolso.
              No es un reporte contable ni de depósitos bancarios. Los pagos de Stripe siguen siendo de prueba
              {data.sales.cardTestOrders
                ? ` (${data.sales.cardTestOrders} pedidos pagados o reembolsados en este período)`
                : ''}
              .
            </p>
            {data.sales.excludedAmounts > 0 && (
              <p role="status" className="mt-3 rounded-xl bg-amber-50 p-3 text-sm">
                {data.sales.excludedAmounts} pedidos pagados o reembolsados se excluyeron de los importes y sus conteos:
                total desconocido o moneda distinta de MXN.
              </p>
            )}
          </section>
          <section aria-label="Pendientes actuales">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
              <h3 className="font-semibold">Pendientes actuales · Todo el historial</h3>
              <button onClick={() => onOrders()} className="text-sm underline">
                Gestionar pedidos →
              </button>
            </div>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
              <Metric label="Sin pago confirmado" value={data.work.pendingPayment} />
              <Metric label="Pagados por preparar" value={data.work.awaitingPreparation} />
              <Metric label="En preparación" value={data.work.preparing} />
              <Metric label="Enviados por entregar" value={data.work.shipped} />
              <Metric label="Cancelaciones / devoluciones por revisar" value={data.work.resolutions} />
            </div>
            <p className="mt-3 text-xs text-stone-500">
              Estos contadores no dependen del período. Un pedido puede requerir atención en más de un apartado. Los
              casos en resolución no se cuentan como entregas listas para avanzar.
            </p>
          </section>
          <section aria-label="Alertas de inventario" className="rounded-2xl border border-stone-200 bg-white p-5">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h3 className="text-lg font-semibold">Alertas de inventario</h3>
              <button onClick={onInventory} className="text-sm underline">
                Administrar inventario →
              </button>
            </div>
            <p className="mt-3 text-sm">
              {data.inventory.totalCount} combinaciones con {data.inventory.threshold} unidades disponibles o menos ·{' '}
              {data.inventory.outOfStockCount} sin disponibilidad.
            </p>
            <p className="mt-2 text-xs text-stone-500">
              Solo productos y variantes activos. La disponibilidad descuenta las reservas vigentes y las pendientes de
              cierre de Stripe.
            </p>
            {data.inventory.productsWithoutVariants > 0 && (
              <p className="mt-4 rounded-xl bg-amber-50 p-3 text-sm">
                {data.inventory.productsWithoutVariants} productos activos no tienen variantes activas. Configura talla,
                color y existencias para venderlos con tarjeta.
              </p>
            )}
            {data.inventory.items.length ? (
              <ul className="mt-4 divide-y divide-stone-200">
                {data.inventory.items.map((v) => (
                  <li key={v.variantId} className="flex flex-wrap items-center justify-between gap-3 py-4">
                    <div className="min-w-0">
                      <p className="break-words font-semibold">{v.productName}</p>
                      <p className="text-sm text-stone-500">
                        {v.size} · {v.color} · Existencias físicas: {v.stock}
                      </p>
                    </div>
                    <span
                      className={`rounded-full px-3 py-2 text-xs font-semibold ${v.availableStock <= 0 ? 'bg-red-50 text-red-800' : 'bg-amber-50 text-amber-800'}`}
                    >
                      Disponibles: {v.availableStock}
                    </span>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="py-6 text-sm text-stone-500">
                {data.inventory.totalCount
                  ? 'No hay más alertas en esta página.'
                  : 'No hay variantes por debajo del umbral seleccionado.'}
              </p>
            )}
            <nav aria-label="Páginas de alertas" className="mt-4 flex flex-wrap items-center justify-between gap-3">
              <button
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
                className="rounded-lg border px-4 py-2 disabled:opacity-40"
              >
                Anterior
              </button>
              <span className="text-sm">
                Página {page} de {Math.max(1, Math.ceil(data.inventory.totalCount / data.inventory.pageSize))}
              </span>
              <button
                disabled={page >= 100000 || page * data.inventory.pageSize >= data.inventory.totalCount}
                onClick={() => setPage((p) => p + 1)}
                className="rounded-lg border px-4 py-2 disabled:opacity-40"
              >
                Siguiente
              </button>
            </nav>
          </section>
          <section aria-label="Pedidos recientes">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
              <h3 className="font-semibold">Últimos pedidos del período</h3>
              <button onClick={() => onOrders({ from: data.from, to: data.to })} className="text-sm underline">
                Ver pedidos del período →
              </button>
            </div>
            {data.recentOrders.length ? (
              <ul className="space-y-3">
                {data.recentOrders.map((o) => (
                  <li key={o.id} className="rounded-xl border border-stone-200 bg-white p-4">
                    <p className="break-all text-sm font-semibold">{o.number}</p>
                    <p className="mt-2 text-xs text-stone-500">
                      {formatOrderDate(o.createdAtUtc)} · Hora local · {paymentMethodLabels[o.paymentMethod]}
                    </p>
                    <div className="mt-3 flex flex-wrap items-center gap-3 text-sm">
                      <span>{paymentLabels[o.paymentStatus]}</span>
                      <span>· {deliveryLabels[o.fulfillmentStatus]}</span>
                      <span className="font-semibold">
                        {o.total == null
                          ? 'Total por acordar'
                          : o.currency === 'MXN'
                            ? `${formatMXN(o.total)} MXN`
                            : `${o.total} ${o.currency}`}
                      </span>
                      <button onClick={() => onOrders({}, o.id)} className="ml-auto underline">
                        Ver pedido
                      </button>
                    </div>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="rounded-xl bg-white p-5 text-sm text-stone-500">Todavía no hay pedidos en este período.</p>
            )}
          </section>
          <p className="text-xs text-stone-500">
            Última consulta: {formatOrderDate(data.generatedAtUtc)} · Hora local. Actualiza para consultar los cambios
            recientes.
          </p>
        </>
      )}
    </section>
  );
}
