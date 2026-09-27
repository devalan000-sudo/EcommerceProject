import React from 'react';

const StoreInformation = React.lazy(() => import('../pages/StoreInformation/StoreInformation.jsx'));
const CheckoutView = React.lazy(() => import('../pages/Checkout/CheckoutView.jsx'));
const EmailSecurityView = React.lazy(() => import('../pages/Account/EmailSecurityView.jsx'));
const OrderView = React.lazy(() => import('../pages/Orders/OrderView.jsx'));
const CatalogView = React.lazy(() => import('../pages/Catalog/CatalogView.jsx'));
const AdminView = React.lazy(() => import('../pages/Admin/AdminView.jsx'));
const ProductDetailView = React.lazy(() => import('../pages/Catalog/ProductDetailView.jsx'));
const AccountView = React.lazy(() => import('../pages/Account/AccountView.jsx'));

function normalize(pathname) {
  return pathname.replace(/\/+$/, '') || '/';
}

export function Routes() {
  const pathname = normalize(window.location.pathname);
  const productRoute = pathname.match(/^\/products\/(\d+)$/);

  return (
    <React.Suspense
      fallback={
        <main className="mx-auto max-w-6xl px-5 py-12" role="status">
          Cargando página…
        </main>
      }
    >
      {pathname === '/informacion' ? (
        <StoreInformation />
      ) : pathname === '/account/security' ? (
        <EmailSecurityView />
      ) : pathname === '/checkout' ? (
        <CheckoutView />
      ) : pathname === '/admin' ? (
        <AdminView />
      ) : pathname === '/account' ? (
        <AccountView />
      ) : productRoute ? (
        <ProductDetailView productId={Number(productRoute[1])} />
      ) : pathname === '/comprar' ? (
        <OrderView />
      ) : (
        <CatalogView />
      )}
    </React.Suspense>
  );
}
