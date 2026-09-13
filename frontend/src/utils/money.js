export const toCents = (amount) => Math.round(Number(amount) * 100);
export const formatMXN = (amount) => new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' }).format(amount);
export const cartTotal = (items) => items.reduce((sum, item) => sum + toCents(item.price) * item.quantity, 0) / 100;
