import { refundLabels } from '../../utils/orders/orders.js';

export default function OrderResolutionNotice({ order }) {
  if (!order?.cancellationPending && !order?.refundStatus) return null;
  return (
    <div role="status" className="my-4 rounded-xl bg-amber-50 p-4 text-sm">
      {order.cancellationPending && (
        <p>El negocio está cancelando este pedido. Se está comprobando el cierre del pago.</p>
      )}
      {order.refundStatus && (
        <>
          <p>
            Devolución: <strong>{refundLabels[order.refundStatus] || 'Por confirmar'}</strong>.
          </p>
          <p className="mt-1">
            {order.refundStatus === 'Succeeded'
              ? 'La devolución total ha sido confirmada. Si pagaste con tarjeta, el tiempo de abono depende de tu banco.'
              : 'La devolución aún no se ha confirmado. Puedes consultar al negocio con tu folio.'}
          </p>
        </>
      )}
    </div>
  );
}
