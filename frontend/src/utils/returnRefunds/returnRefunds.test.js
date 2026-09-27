import test from 'node:test';
import assert from 'node:assert/strict';
import { refundableQuantity, refundAmount } from './returnRefunds.js';

test('pending and uncertain refunds reserve units; only failed refunds release them', () => {
  const item = { orderItemId: 1, receivedQuantity: 5 };
  const request = {
    refunds: ['Pending', 'Succeeded', 'NeedsReview', 'Failed'].map((status) => ({
      status,
      items: [{ orderItemId: 1, quantity: 1 }],
    })),
  };
  assert.equal(refundableQuantity(request, item), 2);
});
test('amount uses original unit prices in cents', () => {
  assert.equal(
    refundAmount(
      [
        { orderItemId: 1, unitPrice: 10.1 },
        { orderItemId: 2, unitPrice: 20.2 },
      ],
      { 1: 2, 2: 1 },
    ),
    40.4,
  );
});
