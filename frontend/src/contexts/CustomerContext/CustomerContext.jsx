import { createContext, useContext } from 'react';
import { api } from '../../services/api/client.js';
import { useCustomerSession } from '../../hooks/useCustomerSession/useCustomerSession.js';

const CustomerContext = createContext(null);
export function CustomerProvider({ children }) {
  return <CustomerContext.Provider value={useCustomerSession(api)}>{children}</CustomerContext.Provider>;
}
export const useCustomer = () => useContext(CustomerContext);
