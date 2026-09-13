import OrderView from './components/OrderView.jsx';
import React from 'react';
import { createRoot } from 'react-dom/client';
import { CartProvider } from './context/CartContext.jsx';
import CatalogView from './components/CatalogView.jsx';
import AdminView from './components/AdminView.jsx';
import ProductDetailView from './components/ProductDetailView.jsx';
import AccountView from './components/AccountView.jsx';
import { CustomerProvider } from './context/CustomerContext.jsx';
import './styles.css';

const productRoute = window.location.pathname.match(/^\/products\/(\d+)\/?$/);

createRoot(document.getElementById('root')).render(
  <React.StrictMode><CustomerProvider><CartProvider>{window.location.pathname.replace(/\/+$/, '') === '/admin' ? <AdminView /> : window.location.pathname.replace(/\/+$/, '') === '/account' ? <AccountView /> : productRoute ? <ProductDetailView productId={Number(productRoute[1])} /> : window.location.pathname.replace(/\/+$/, '') === '/comprar' ? <OrderView /> : <CatalogView />}</CartProvider></CustomerProvider></React.StrictMode>,
);




