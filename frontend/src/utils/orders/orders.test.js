import test from 'node:test';
import assert from 'node:assert/strict';
import {
  canResumePayment,
  formatOrderDate,
  isOrderId,
  orderCheckoutUrl,
  orderContactLink,
  resolutionAction,
} from './orders.js';

const id = '11111111-2222-3333-4444-555555555555';
test('cancelación y devolución distinguen dinero, envío y solicitudes existentes', () => {
  const order = {
    id,
    paymentMethod: 'Card',
    paymentStatus: 'Pending',
    fulfillmentStatus: 'PendingConfirmation',
    total: 100,
  };
  assert.equal(resolutionAction(order), 'cancel');
  assert.equal(resolutionAction({ ...order, paymentStatus: 'Paid' }), 'refund');
  for (const fulfillmentStatus of ['Shipped', 'Delivered', 'Cancelled'])
    assert.equal(resolutionAction({ ...order, fulfillmentStatus }), null);
  assert.equal(resolutionAction({ ...order, refundStatus: 'Pending' }), null);
  assert.equal(resolutionAction({ ...order, cancellationPending: true }), null);
  assert.equal(canResumePayment({ ...order, cancellationPending: true }), false);
  assert.equal(canResumePayment({ ...order, refundStatus: 'Pending' }), false);
});
test('solo retoma tarjeta pendiente de pago y confirmación; conserva el pedido original', () => {
  const order = { id, paymentMethod: 'Card', paymentStatus: 'Pending', fulfillmentStatus: 'PendingConfirmation' };
  assert.equal(canResumePayment(order), true);
  assert.equal(orderCheckoutUrl(id), `/checkout?order=${id}`);
  for (const paymentStatus of ['Paid', 'Failed', 'Refunded'])
    assert.equal(canResumePayment({ ...order, paymentStatus }), false);
  for (const fulfillmentStatus of ['Preparing', 'Shipped', 'Delivered', 'Cancelled'])
    assert.equal(canResumePayment({ ...order, fulfillmentStatus }), false);
  assert.equal(canResumePayment({ ...order, paymentMethod: 'WhatsApp' }), false);
  assert.equal(canResumePayment({ ...order, id: '../admin' }), false);
  assert.equal(canResumePayment(null), false);
});
test('el retorno de login solo admite identificadores de pedido; no destinos externos', () => {
  assert.equal(isOrderId(id), true);
  for (const invalid of ['', null, 'https://example.com', `${id}&payment=success`, '../admin']) {
    assert.equal(isOrderId(invalid), false);
    assert.equal(orderCheckoutUrl(invalid), '/checkout');
  }
});
test('el contacto de seguimiento incluye el folio codificado y valida el teléfono', () => {
  const url = new URL(orderContactLink({ number: 'EB-123 & prueba' }, '+52 (55) 1234-5678'));
  assert.equal(url.hostname, 'wa.me');
  assert.equal(url.pathname, '/525512345678');
  assert.equal(
    url.searchParams.get('text'),
    'Hola, quisiera consultar mi pedido EB-123 & prueba. ¿Me ayudan con su seguimiento?',
  );
  assert.throws(() => orderContactLink({ number: 'EB-123' }, ''), /número de WhatsApp/);
});
test('las fechas sin zona del backend se interpretan como UTC y toleran datos inválidos', () => {
  assert.equal(formatOrderDate('2026-09-14T02:00:00'), formatOrderDate('2026-09-14T02:00:00Z'));
  assert.equal(formatOrderDate('2026-09-13T20:00:00-06:00'), formatOrderDate('2026-09-14T02:00:00Z'));
  assert.equal(formatOrderDate('invalid'), 'Fecha no disponible');
});
