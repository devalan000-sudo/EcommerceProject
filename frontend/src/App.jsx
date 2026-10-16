import React from 'react';
import { CartProvider } from './contexts/CartContext/CartContext.jsx';
import { CustomerProvider } from './contexts/CustomerContext/CustomerContext.jsx';
import { StoreProvider } from './contexts/StoreContext/StoreContext.jsx';
import { Routes } from './routes/routes.jsx';

export default function App() {
  return (
    <React.StrictMode>
      <StoreProvider>
        <CustomerProvider>
          <CartProvider>
            <Routes />
          </CartProvider>
        </CustomerProvider>
      </StoreProvider>
    </React.StrictMode>
  );
}
