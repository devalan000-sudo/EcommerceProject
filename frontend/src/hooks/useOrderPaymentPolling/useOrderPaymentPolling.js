import { useEffect } from 'react';
import { getCustomerOrder } from '../../services/orders/ordersService.js';

export function useOrderPaymentPolling(api, order, session, onUpdate) {
  useEffect(() => {
    if (!order || order.paymentMethod !== 'Card' || order.paymentStatus !== 'Pending' || !session) return undefined;
    let attempts = 0;
    const timer = setInterval(() => {
      if (++attempts > 20) {
        clearInterval(timer);
        return;
      }
      getCustomerOrder(api, session, order.id)
        .then((response) => onUpdate(response.data))
        .catch(() => {});
    }, 3000);
    return () => clearInterval(timer);
  }, [api, order?.id, order?.paymentMethod, order?.paymentStatus, session, onUpdate]);
}
