import { formatMXN } from '../../utils/money/money.js';

export default function CheckoutItemsList({ items }) {
  return (
    <ul className="my-4 divide-y divide-stone-200">
      {items.map((item) => (
        <li key={`${item.id}:${item.variantId || ''}`} className="py-2 text-sm">
          {item.quantity} × {item.name} · {[item.size, item.color].filter(Boolean).join(' / ')}{' '}
          <span className="text-stone-500">{formatMXN(item.price)} c/u</span>
        </li>
      ))}
    </ul>
  );
}
