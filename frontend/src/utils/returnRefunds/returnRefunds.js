export function refundableQuantity(request, item) {
  const claimed = (request.refunds || [])
    .filter((r) => r.status !== 'Failed')
    .flatMap((r) => r.items)
    .filter((i) => i.orderItemId === item.orderItemId)
    .reduce((sum, i) => sum + i.quantity, 0);
  return Math.max(0, item.receivedQuantity - claimed);
}
export function refundAmount(items, quantities) {
  return (
    items.reduce((sum, i) => sum + Math.round(i.unitPrice * 100) * Number(quantities[i.orderItemId] || 0), 0) / 100
  );
}
