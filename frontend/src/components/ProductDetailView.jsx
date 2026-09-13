import { businessName } from '../config/business.js';
import { useEffect, useRef, useState } from 'react';
import { api } from '../api/client.js';
import { useCart } from '../context/CartContext.jsx';
import { isDemoCatalog } from '../data/catalogMode.js';
import { demoProducts, demoCategories } from '../data/demoCatalog.js';
import { formatMXN } from '../utils/money.js';
import StoreHeader from './StoreHeader.jsx';
import CartModal from './CartModal.jsx';
import ProductGallery from './ProductGallery.jsx';
import { registerProductView } from '../data/productViews.js';

export default function ProductDetailView({ productId }) {
  const [product, setProduct] = useState(null);
  const [categories, setCategories] = useState([]);
  const [status, setStatus] = useState('loading');
  const [attempt, setAttempt] = useState(0);
  const [search, setSearch] = useState('');
  const [cartOpen, setCartOpen] = useState(false);
  const [cartReady, setCartReady] = useState(false);
  const [added, setAdded] = useState(false);
  const timer = useRef(null);
  const trackedProduct = useRef(null);
  const { add, syncProducts } = useCart();

  useEffect(() => () => clearTimeout(timer.current), []);
  useEffect(() => {
    const controller = new AbortController();
    setStatus('loading'); setCartReady(false);
    if (isDemoCatalog) {
      const item = demoProducts.find(p => p.id === productId && p.isActive);
      setProduct(item || null); setCategories(demoCategories); syncProducts(demoProducts);
      setCartReady(true); setStatus(item ? 'ready' : 'missing');
      return () => controller.abort();
    }
    api.get(`/products/${productId}`, { signal: controller.signal })
      .then(({ data }) => { setProduct(data); setStatus(data.isActive ? 'ready' : 'missing'); })
      .catch(error => { if (!controller.signal.aborted) setStatus(error.response?.status === 404 ? 'missing' : 'error'); });
    Promise.all([api.get('/categories', { signal: controller.signal }), api.get('/products', { signal: controller.signal })])
      .then(([c, p]) => { setCategories(c.data); syncProducts(p.data); setCartReady(true); })
      .catch(() => { /* Mantener el envío deshabilitado hasta actualizar el catálogo. */ });
    return () => controller.abort();
  }, [productId, attempt, syncProducts]);
  useEffect(() => {
    if (status !== 'ready' || product?.id !== productId || trackedProduct.current === productId) return;
    trackedProduct.current = productId;
    void registerProductView(productId);
  }, [status, product, productId]);
  useEffect(() => {
    const oldTitle = document.title;
    document.title = businessName;
    return () => { document.title = oldTitle; };
  }, [product, businessName]);
  function addProduct() {
    add(product); setAdded(true); clearTimeout(timer.current);
    timer.current = setTimeout(() => setAdded(false), 1800);
  }
  return <>
    <StoreHeader businessName={businessName} search={search} onSearchChange={setSearch} onSearchSubmit={() => { window.location.href = `/?search=${encodeURIComponent(search)}#catalog`; }} onOpenCart={() => setCartOpen(true)} onSelectCategory={id => { window.location.href = `/?category=${encodeURIComponent(id)}#catalog`; }} />
    <main className="mx-auto max-w-6xl px-5 py-6 sm:py-10">
      <nav aria-label="Ruta del producto" className="mb-7 flex flex-wrap gap-2 text-xs text-stone-500"><a href="/" className="underline">Inicio</a><span aria-hidden="true">/</span><a href="/#catalog" className="underline">Productos</a>{product && <><span aria-hidden="true">/</span><span aria-current="page">{product.name}</span></>}</nav>
      {status === 'loading' && <p role="status" className="py-20">Cargando producto…</p>}
      {status === 'missing' && <section className="rounded-2xl border border-stone-200 p-10"><h1 className="text-2xl font-semibold">Producto no disponible</h1><p className="mt-3 text-stone-500">Este artículo ya no está disponible o el enlace no existe.</p><a href="/#catalog" className="mt-5 inline-block underline">Volver al catálogo</a></section>}
      {status === 'error' && <div role="alert" className="rounded-2xl bg-red-50 p-8"><h1 className="text-xl font-semibold">No pudimos cargar el producto</h1><button onClick={() => setAttempt(n => n + 1)} className="mt-4 underline">Intentar nuevamente</button></div>}
      {status === 'ready' && product && <div className="grid items-start gap-8 lg:grid-cols-2 lg:gap-16">
        <ProductGallery key={product.id} product={product} />
        <section className="lg:sticky lg:top-44">
          <a href={`/?category=${product.categoryId}#catalog`} className="text-xs font-semibold uppercase tracking-[0.2em] text-stone-500 hover:underline">{product.categoryName}</a>
          <h1 className="mt-4 text-4xl font-semibold leading-tight tracking-tight sm:text-5xl">{product.name}</h1>
          <p className="mt-5 text-2xl font-semibold">{formatMXN(product.price)} <span className="text-sm font-normal text-stone-500">MXN</span></p>
          {isDemoCatalog && <p className="mt-3 text-xs text-stone-500">Artículo de muestra · Imagen generada con IA y precio ilustrativo.</p>}
          <div className="my-7 border-y border-stone-200 py-6"><h2 className="text-sm font-semibold">Acerca de este artículo</h2><p className="mt-3 whitespace-pre-line text-base leading-relaxed text-stone-600">{product.description || 'Consulta los detalles de este artículo directamente con el negocio.'}</p></div>
          <button onClick={addProduct} className="flex min-h-14 w-full items-center justify-between rounded-xl bg-brand px-6 py-4 font-semibold text-white hover:bg-emerald-900"><span aria-live="polite">{added ? 'Agregado al carrito' : 'Agregar al carrito'}</span><span aria-hidden="true">{added ? '✓' : '+'}</span></button>
          <button onClick={() => setCartOpen(true)} className="mt-3 min-h-11 w-full rounded-xl border border-brand/20 p-3 text-sm font-semibold hover:bg-white">Ver mi carrito</button>
          {!cartReady && <p role="status" className="mt-3 text-sm text-stone-500">No se ha actualizado el catálogo para realizar el pedido. <button onClick={() => setAttempt(n => n + 1)} className="underline">Reintentar</button></p>}
          <details className="mt-7 border-b border-stone-200 py-4" open><summary className="cursor-pointer text-sm font-semibold">Tallas y disponibilidad</summary><p className="mt-3 text-sm leading-relaxed text-stone-500">Confirma las tallas, colores y existencias con el negocio por WhatsApp antes de acordar tu compra.</p></details>
          <details className="border-b border-stone-200 py-4"><summary className="cursor-pointer text-sm font-semibold">Entrega y forma de pago</summary><p className="mt-3 text-sm leading-relaxed text-stone-500">Comparte tu zona para consultar cobertura y costo de entrega. El envío se confirma por separado y no está incluido en el precio del artículo.</p></details>
          <a href="/#catalog" className="mt-7 inline-block py-2 text-sm underline underline-offset-4">← Seguir explorando</a>
        </section>
      </div>}
    </main>
    <CartModal open={cartOpen} onClose={() => setCartOpen(false)} catalogReady={cartReady} />
  </>;
}


