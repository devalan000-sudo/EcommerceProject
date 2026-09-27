import { formatMXN } from '../../utils/money/money.js';

export default function CheckoutTotals({ summary }) {
  return (
    <>
      <p>Productos: {formatMXN(summary.subtotal)}</p>
      <p>
        Entrega:{' '}
        {summary.shippingCost == null
          ? 'Por acordar con la tienda'
          : summary.shippingCost === 0
            ? 'Sin cargo en este checkout'
            : formatMXN(summary.shippingCost)}
      </p>
      <p className="mt-3 text-xl font-semibold">
        Total: {summary.total == null ? 'Pendiente de entrega' : `${formatMXN(summary.total)} MXN`}
      </p>
    </>
  );
}
