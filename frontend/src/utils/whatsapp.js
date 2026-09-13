import { cartTotal, formatMXN } from './money.js';

export function generateWhatsAppMessage(cartItems, total, simulation = false) {
  if (!Array.isArray(cartItems) || !cartItems.length) throw new Error('El carrito está vacío.');
  for (const item of cartItems) {
    if (!Number.isInteger(item.quantity) || item.quantity < 1 || !Number.isFinite(item.price) || item.price <= 0 || typeof item.name !== 'string')
      throw new Error('El carrito contiene un producto inválido.');
  }
  if (!Number.isFinite(total) || Math.round(total * 100) !== Math.round(cartTotal(cartItems) * 100))
    throw new Error('El total no coincide con los productos del carrito.');
  const lines = cartItems.map(item => {
    const details = typeof item.details === 'string' ? item.details.replace(/[\r\n]+/g, ' ').trim().slice(0, 200) : '';
    return `- ${item.quantity}x ${item.name.replace(/[\r\n]+/g, ' ')}${details ? `: ${details}` : ''} (${formatMXN(item.price)} c/u)`;
  });
  return `${simulation ? '[SIMULACIÓN · Catálogo de muestra]\n' : ''}Hola, me interesa comprar:\n${lines.join('\n')}\nTotal de productos: ${formatMXN(total)} MXN\n¿Me confirman disponibilidad y costo de envío?`;
}

export function generateWhatsAppLink(cartItems, total, businessPhone, simulation = false) {
  const phone = String(businessPhone ?? '').replace(/[\s()+-]/g, '');
  if (!/^[1-9]\d{7,14}$/.test(phone)) throw new Error('Configura un número de WhatsApp válido con código de país.');
  const message = generateWhatsAppMessage(cartItems, total, simulation);
  return `https://wa.me/${phone}?text=${encodeURIComponent(message)}`;
}
