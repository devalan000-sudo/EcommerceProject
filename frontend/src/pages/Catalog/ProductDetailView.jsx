import VariantSelector from './VariantSelector.jsx';
import { availableVariant, imageForColor } from '../../utils/variants/variantOptions.js';
import { useStore } from '../../contexts/StoreContext/StoreContext.jsx';
import { useEffect, useRef, useState } from 'react';
import { useCart } from '../../contexts/CartContext/CartContext.jsx';
import { isDemoCatalog } from '../../data/catalogMode.js';
import { demoProducts } from '../../data/demoCatalog.js';
import { formatMXN } from '../../utils/money/money.js';
import StoreHeader from '../../components/layout/StoreHeader.jsx';
import CartModal from '../../components/cart/CartModal.jsx';
import ProductGallery from './ProductGallery.jsx';
import { registerProductView } from '../../data/productViews.js';
import { loadCatalog, loadProduct } from '../../services/catalog/catalogService.js';

export default function ProductDetailView({ productId }) {
  const { businessName } = useStore();
  const [product, setProduct] = useState(null);
  const [status, setStatus] = useState('loading');
  const [attempt, setAttempt] = useState(0);
  const [search, setSearch] = useState('');
  const [cartOpen, setCartOpen] = useState(false);
  const [cartReady, setCartReady] = useState(false);
  const [added, setAdded] = useState(false);
  const [color, setColor] = useState('');
  const [size, setSize] = useState('');
  const selectedVariant = availableVariant(product?.variants || [], color, size);
  const colorImage = product ? imageForColor(product, color) : null;
  const needsVariant = !isDemoCatalog || !!(product?.hasVariants || product?.variants?.length);
  const timer = useRef(null);
  const trackedProduct = useRef(null);
  const { add, syncProducts } = useCart();

  useEffect(() => () => clearTimeout(timer.current), []);
  useEffect(() => {
    const available = product?.variants?.filter((v) => v.isActive && v.availableStock > 0) || [];
    setColor(available.length === 1 ? available[0].color : '');
    setSize(available.length === 1 ? available[0].size : '');
  }, [product]);
  useEffect(() => {
    const controller = new AbortController();
    setStatus('loading');
    setCartReady(false);
    if (isDemoCatalog) {
      const item = demoProducts.find((p) => p.id === productId && p.isActive);
      setProduct(item || null);
      syncProducts(demoProducts);
      setCartReady(true);
      setStatus(item ? 'ready' : 'missing');
      return () => controller.abort();
    }
    loadProduct(productId, controller.signal)
      .then(({ data }) => {
        setProduct(data);
        setStatus(data.isActive ? 'ready' : 'missing');
      })
      .catch((error) => {
        if (!controller.signal.aborted) setStatus(error.response?.status === 404 ? 'missing' : 'error');
      });
    loadCatalog(controller.signal)
      .then(([, p]) => {
        syncProducts(p.data);
        setCartReady(true);
      })
      .catch(() => {
        /* Mantener el envío deshabilitado hasta actualizar el catálogo. */
      });
    return () => controller.abort();
  }, [productId, attempt, syncProducts]);
  useEffect(() => {
    if (status !== 'ready' || product?.id !== productId || trackedProduct.current === productId) return;
    trackedProduct.current = productId;
    void registerProductView(productId);
  }, [status, product, productId]);
  useEffect(() => {
    const oldTitle = document.title;
    if (!product && document.querySelector('meta[name="seo-rendered"]')) return;
    document.title = product ? `${product.name} | ${businessName}` : businessName;
    return () => {
      document.title = oldTitle;
    };
  }, [product, businessName]);
  function addProduct() {
    if (needsVariant && (!selectedVariant || selectedVariant.availableStock < 1)) return;
    add({
      ...product,
      ...(selectedVariant
        ? {
            imageUrl: selectedVariant.imageUrl || product.imageUrl,
            variantId: selectedVariant.id,
            size: selectedVariant.size,
            color: selectedVariant.color,
            availableStock: selectedVariant.availableStock,
          }
        : {}),
    });
    setAdded(true);
    clearTimeout(timer.current);
    timer.current = setTimeout(() => setAdded(false), 1800);
  }
  return (
    <>
      <StoreHeader
        businessName={businessName}
        search={search}
        onSearchChange={setSearch}
        onSearchSubmit={() => {
          window.location.href = `/?search=${encodeURIComponent(search)}#catalog`;
        }}
        onOpenCart={() => setCartOpen(true)}
        onSelectCategory={(id) => {
          window.location.href = `/?category=${encodeURIComponent(id)}#catalog`;
        }}
      />
      <main className="mx-auto max-w-6xl px-5 py-6 sm:py-10">
        <nav aria-label="Ruta del producto" className="mb-7 flex flex-wrap gap-2 text-xs text-stone-500">
          <a href="/" className="underline">
            Inicio
          </a>
          <span aria-hidden="true">/</span>
          <a href="/#catalog" className="underline">
            Productos
          </a>
          {product && (
            <>
              <span aria-hidden="true">/</span>
              <span aria-current="page">{product.name}</span>
            </>
          )}
        </nav>
        {status === 'loading' && (
          <p role="status" className="py-20">
            Cargando producto…
          </p>
        )}
        {status === 'missing' && (
          <section className="rounded-2xl border border-stone-200 p-10">
            <h1 className="text-2xl font-semibold">Producto no disponible</h1>
            <p className="mt-3 text-stone-500">Este artículo ya no está disponible o el enlace no existe.</p>
            <a href="/#catalog" className="mt-5 inline-block underline">
              Volver al catálogo
            </a>
          </section>
        )}
        {status === 'error' && (
          <div role="alert" className="rounded-2xl bg-red-50 p-8">
            <h1 className="text-xl font-semibold">No pudimos cargar el producto</h1>
            <button onClick={() => setAttempt((n) => n + 1)} className="mt-4 underline">
              Intentar nuevamente
            </button>
          </div>
        )}
        {status === 'ready' && product && (
          <div className="grid items-start gap-8 lg:grid-cols-2 lg:gap-16">
            <div>
              <ProductGallery
                key={`${product.id}:${color}:${colorImage}`}
                product={{
                  ...product,
                  imageUrl: colorImage || product.imageUrl,
                  imageUrls: colorImage ? [] : product.imageUrls,
                }}
              />
              {color && !colorImage && (
                <p className="mt-3 text-sm text-stone-500">
                  Foto de referencia del producto. Aún no hay una fotografía específica para {color}.
                </p>
              )}
            </div>
            <section className="lg:sticky lg:top-44">
              <a
                href={`/?category=${product.categoryId}#catalog`}
                className="text-xs font-semibold uppercase tracking-[0.2em] text-stone-500 hover:underline"
              >
                {product.categoryName}
              </a>
              <h1 className="mt-4 text-4xl font-semibold leading-tight tracking-tight sm:text-5xl">{product.name}</h1>
              <p className="mt-5 text-2xl font-semibold">
                {formatMXN(product.price)} <span className="text-sm font-normal text-stone-500">MXN</span>
              </p>
              {isDemoCatalog && (
                <p className="mt-3 text-xs text-stone-500">
                  Artículo de muestra · Imagen generada con IA y precio ilustrativo.
                </p>
              )}
              <div className="my-7 border-y border-stone-200 py-6">
                <h2 className="text-sm font-semibold">Acerca de este artículo</h2>
                <p className="mt-3 whitespace-pre-line text-base leading-relaxed text-stone-600">
                  {product.description || 'Consulta los detalles de este artículo directamente con el negocio.'}
                </p>
              </div>
              {needsVariant && (
                <VariantSelector
                  variants={product.variants || []}
                  color={color}
                  size={size}
                  onColor={(next) => {
                    setColor(next);
                    setSize('');
                    setAdded(false);
                  }}
                  onSize={(next) => {
                    setSize(next);
                    setAdded(false);
                  }}
                />
              )}
              {selectedVariant && (
                <p className="mb-4 text-sm">
                  {selectedVariant.availableStock} unidades disponibles en esta combinación.
                </p>
              )}
              {!needsVariant && !isDemoCatalog && (
                <p className="mb-4 text-sm text-stone-500">
                  Disponibilidad por confirmar con el negocio. Este producto aún no tiene variantes configuradas para
                  pago con tarjeta.
                </p>
              )}
              <button
                disabled={needsVariant && (!selectedVariant || selectedVariant.availableStock < 1)}
                onClick={addProduct}
                className="flex min-h-14 w-full items-center justify-between rounded-xl bg-brand px-6 py-4 font-semibold text-white hover:bg-emerald-900 disabled:opacity-40"
              >
                <span aria-live="polite">{added ? 'Agregado al carrito' : 'Agregar al carrito'}</span>
                <span aria-hidden="true">{added ? '✓' : '+'}</span>
              </button>
              <button
                onClick={() => setCartOpen(true)}
                className="mt-3 min-h-11 w-full rounded-xl border border-brand/20 p-3 text-sm font-semibold hover:bg-white"
              >
                Ver mi carrito
              </button>
              {!cartReady && (
                <p role="status" className="mt-3 text-sm text-stone-500">
                  No se ha actualizado el catálogo para realizar el pedido.{' '}
                  <button onClick={() => setAttempt((n) => n + 1)} className="underline">
                    Reintentar
                  </button>
                </p>
              )}
              <details className="mt-7 border-b border-stone-200 py-4" open>
                <summary className="cursor-pointer text-sm font-semibold">Tallas y disponibilidad</summary>
                <p className="mt-3 text-sm leading-relaxed text-stone-500">
                  {needsVariant
                    ? 'Selecciona una combinación disponible. El carrito no reserva unidades; volveremos a comprobar las existencias al continuar con tu compra.'
                    : 'Consulta las tallas, colores y disponibilidad de este artículo con el negocio por WhatsApp.'}
                </p>
              </details>
              <details className="border-b border-stone-200 py-4">
                <summary className="cursor-pointer text-sm font-semibold">Entrega y forma de pago</summary>
                <p className="mt-3 text-sm leading-relaxed text-stone-500">
                  Consulta las opciones de entrega y su costo al finalizar tu compra. Revisa el total antes de confirmar
                  y elige tarjeta, cuando esté habilitada, o coordina el pedido por WhatsApp.
                </p>
              </details>
              <a href="/#catalog" className="mt-7 inline-block py-2 text-sm underline underline-offset-4">
                ← Seguir explorando
              </a>
            </section>
          </div>
        )}
      </main>
      <CartModal open={cartOpen} onClose={() => setCartOpen(false)} catalogReady={cartReady} />
    </>
  );
}
