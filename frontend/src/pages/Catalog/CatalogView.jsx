import { StoreInformationLinks } from '../StoreInformation/StoreInformation.jsx';
import StoreImage from '../../components/layout/StoreImage.jsx';
import { normalize } from '../../utils/search/search.js';
import { useStore } from '../../contexts/StoreContext/StoreContext.jsx';
import ShoppingSteps from '../../components/layout/ShoppingSteps.jsx';
import { featuredProducts } from '../../utils/productViews/productViews.js';
import { withDemoViews } from '../../data/productViews.js';
import { useEffect, useState } from 'react';
import { catalogErrorMessage } from '../../utils/catalogRequest/catalogRequest.js';
import { useCart } from '../../contexts/CartContext/CartContext.jsx';
import ProductCard from './ProductCard.jsx';
import CartModal from '../../components/cart/CartModal.jsx';
import StoreHeader from '../../components/layout/StoreHeader.jsx';
import CollectionSection from './CollectionSection.jsx';
import { isDemoCatalog } from '../../data/catalogMode.js';
import { demoProducts, demoCategories, demoHeroImage } from '../../data/demoCatalog.js';
import { loadCatalog } from '../../services/catalog/catalogService.js';

export default function CatalogView() {
  const { businessName, settings } = useStore();
  const [products, setProducts] = useState([]);
  const [categories, setCategories] = useState([]);
  const [category, setCategory] = useState(() => new URLSearchParams(window.location.search).get('category') || '');
  const [search, setSearch] = useState(() => new URLSearchParams(window.location.search).get('search') || '');
  const [sort, setSort] = useState('default');
  const [showAll, setShowAll] = useState(false);
  const [status, setStatus] = useState('loading');
  const [loadError, setLoadError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [cartOpen, setCartOpen] = useState(false);
  const { syncProducts } = useCart();

  useEffect(() => {
    if (isDemoCatalog) {
      setProducts(withDemoViews(demoProducts));
      setCategories(demoCategories);
      syncProducts(demoProducts);
      setStatus('ready');
      return;
    }
    const controller = new AbortController();
    setStatus('loading');
    setLoadError('');
    loadCatalog(controller.signal)
      .then(([p, c]) => {
        if (controller.signal.aborted) return;
        setProducts(p.data);
        syncProducts(p.data);
        setCategories(c.data);
        setStatus('ready');
      })
      .catch((error) => {
        if (!controller.signal.aborted) {
          setLoadError(catalogErrorMessage(error));
          setStatus('error');
        }
      });
    return () => controller.abort();
  }, [attempt, syncProducts]);
  const visible = products.filter(
    (p) =>
      p.isActive &&
      (!category || p.categoryId === Number(category)) &&
      normalize(p.name + ' ' + p.description).includes(normalize(search.trim())),
  );

  const isFeatured = !showAll && !category && !search.trim();
  const selection = isFeatured ? featuredProducts(visible) : visible;
  const sorted = [...selection].sort((a, b) =>
    sort === 'price-asc'
      ? a.price - b.price
      : sort === 'price-desc'
        ? b.price - a.price
        : sort === 'name'
          ? a.name.localeCompare(b.name, 'es')
          : 0,
  );
  function chooseCollection(id) {
    setCategory(id);
    setSearch('');
    document.getElementById('catalog')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    document.getElementById('catalog')?.focus({ preventScroll: true });
  }

  return (
    <>
      <div className="bg-brand px-5 py-2.5 text-center text-[10px] font-medium uppercase tracking-[0.18em] text-white sm:text-xs">
        {settings.announcement}
      </div>
      <a
        href="#catalog"
        className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50 focus:rounded-lg focus:bg-white focus:p-4"
      >
        Ir al catálogo
      </a>
      <StoreHeader
        businessName={businessName}
        search={search}
        onSearchChange={setSearch}
        onOpenCart={() => setCartOpen(true)}
        onSelectCategory={(id) => {
          setCategory(id);
          setSearch('');
        }}
      />
      <main className="mx-auto max-w-6xl px-5 pb-16">
        <section className="my-5 grid overflow-hidden rounded-3xl bg-[#182320] text-white md:grid-cols-2">
          <div className="flex flex-col justify-center px-6 py-12 sm:p-12 lg:p-14">
            <p className="text-xs font-semibold uppercase tracking-[0.25em] text-stone-300">
              Ropa de hombre · Estilo urbano
            </p>
            <h1 className="mt-5 whitespace-pre-line break-words text-5xl font-bold leading-[0.98] tracking-[-0.055em] sm:text-7xl lg:text-8xl">
              {settings.heroTitle.split('\n').map((line, index) => (
                <span key={index} className={index ? 'block text-[var(--color-accent)]' : 'block'}>
                  {line}
                </span>
              ))}
            </h1>
            <p className="mt-5 max-w-sm whitespace-pre-line break-words text-sm leading-relaxed text-stone-300">
              {settings.heroDescription}
            </p>
            <a
              href="#catalog"
              className="mt-7 inline-flex min-h-11 items-center gap-6 self-start rounded-full bg-[var(--color-accent)] px-6 py-3 text-sm font-semibold text-[#182320] hover:bg-white"
            >
              Explorar colección <span aria-hidden="true">↗</span>
            </a>
          </div>
          {!settings.heroImageUrl && isDemoCatalog && (
            <div className="relative min-h-72 overflow-hidden md:min-h-[480px]">
              <img
                src={demoHeroImage}
                alt="Inspiración de estilo masculino con chamarra de mezclilla"
                className="absolute inset-0 h-full w-full object-cover object-top"
                fetchPriority="high"
              />
              <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/70 to-transparent p-6 pt-16">
                <p className="text-xs uppercase tracking-widest">Esenciales urbanos</p>
                <p className="mt-1 text-xl font-semibold">Hazlo a tu manera.</p>
              </div>
            </div>
          )}
          <StoreImage
            src={settings.heroImageUrl}
            alt={`Colección de ${businessName}`}
            className="h-full min-h-72 w-full object-cover md:min-h-[480px]"
            fallback={
              !isDemoCatalog || settings.heroImageUrl ? (
                <div className="flex items-center justify-center break-words p-10 text-5xl font-bold tracking-tighter text-[var(--color-accent)]">
                  EVERYDAY
                  <br />
                  ESSENTIALS.
                </div>
              ) : null
            }
          />
        </section>
        {isDemoCatalog && (
          <p className="mb-6 text-xs text-stone-500">
            Catálogo de muestra · Imágenes de producto generadas con IA y precios ilustrativos. No disponible para
            compra.
          </p>
        )}
        <ShoppingSteps />
        {status === 'ready' && (
          <CollectionSection categories={categories} products={products} onSelect={chooseCollection} />
        )}
        <div className="mb-8 flex gap-2 overflow-x-auto pb-2" aria-label="Colecciones de ropa">
          <button
            onClick={() => {
              setShowAll(false);
              setCategory('');
              setSearch('');
            }}
            aria-pressed={isFeatured}
            className={`shrink-0 rounded-full border px-5 py-2.5 text-sm ${isFeatured ? 'border-brand bg-brand text-white' : 'border-stone-300 bg-white'}`}
          >
            Destacados
          </button>
          <button
            onClick={() => {
              setCategory('');
              setSearch('');
              setShowAll(true);
            }}
            aria-pressed={showAll && !category}
            className={`shrink-0 rounded-full border px-5 py-2.5 text-sm ${showAll && !category ? 'border-brand bg-brand text-white' : 'border-stone-300 bg-white'}`}
          >
            Ver todo
          </button>
          {categories.map((c) => (
            <button
              key={c.id}
              onClick={() => {
                setCategory(String(c.id));
                setSearch('');
              }}
              aria-pressed={category === String(c.id)}
              className={`shrink-0 rounded-full border px-5 py-2.5 text-sm ${category === String(c.id) ? 'border-brand bg-brand text-white' : 'border-stone-300 bg-white'}`}
            >
              {c.name}
            </button>
          ))}
        </div>
        <div
          id="catalog"
          tabIndex={-1}
          className="scroll-mt-56 md:scroll-mt-40 mb-7 flex flex-wrap items-end justify-between gap-5 border-t border-black/10 pt-8"
        >
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-stone-500">La selección</p>
            <h2 className="mt-2 text-3xl font-semibold tracking-tight">
              {categories.find((c) => String(c.id) === category)?.name ||
                (isFeatured ? 'Esenciales para tu día.' : 'Todos los productos.')}
            </h2>
            <p role="status" className="mt-2 text-sm text-stone-500">
              {status === 'ready'
                ? `${selection.length} productos${search ? ` para “${search}”` : ''}`
                : 'Preparando el catálogo'}
            </p>
            {isFeatured && (
              <p className="mt-2 text-sm text-stone-500">Los favoritos del catálogo, ordenados por visitas.</p>
            )}
          </div>
          <label className="text-xs text-stone-500">
            Ordenar por{' '}
            <select
              value={sort}
              onChange={(e) => setSort(e.target.value)}
              className="ml-2 rounded-lg border border-stone-200 bg-white p-3 text-sm text-brand"
            >
              <option value="default">Selección original</option>
              <option value="price-asc">Menor precio</option>
              <option value="price-desc">Mayor precio</option>
              <option value="name">Nombre: A–Z</option>
            </select>
          </label>
        </div>
        {(category || search) && (
          <button
            onClick={() => {
              setCategory('');
              setSearch('');
            }}
            className="mb-5 rounded-full border border-brand/15 px-4 py-2 text-xs hover:bg-white"
          >
            Limpiar filtros ×
          </button>
        )}
        {status === 'loading' && (
          <p role="status" className="py-12 text-stone-500">
            Cargando catálogo…
          </p>
        )}
        {status === 'error' && (
          <div role="alert" className="rounded-xl border border-red-200 bg-white p-6">
            <p>{loadError}</p>
            <button onClick={() => setAttempt((n) => n + 1)} className="mt-4 font-semibold underline">
              Intentar de nuevo
            </button>
          </div>
        )}
        {status === 'ready' &&
          (visible.length ? (
            <div className="grid grid-cols-1 gap-x-5 gap-y-9 min-[400px]:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
              {sorted.map((p) => (
                <ProductCard key={p.id} product={p} />
              ))}
            </div>
          ) : (
            <p className="rounded-xl border border-dashed border-stone-300 p-10 text-center text-stone-500">
              No hay productos que coincidan con tu búsqueda.
            </p>
          ))}
      </main>
      <footer className="bg-brand text-white">
        <div className="mx-auto grid max-w-6xl gap-10 px-5 py-12 sm:grid-cols-2 lg:grid-cols-[2fr_1fr_1fr]">
          <div>
            <p className="break-words text-3xl font-bold tracking-tight">
              {businessName}
              <span className="text-[var(--color-accent)]">.</span>
            </p>
            <p className="mt-3 max-w-xs whitespace-pre-line break-words text-sm leading-relaxed text-white/65">
              {settings.footerDescription}
            </p>
          </div>
          <div>
            <h2 className="text-xs font-semibold uppercase tracking-widest text-white/55">Colecciones</h2>
            <ul className="mt-4 space-y-2">
              {categories.map((c) => (
                <li key={c.id}>
                  <button onClick={() => chooseCollection(String(c.id))} className="py-1 text-sm hover:underline">
                    {c.name}
                  </button>
                </li>
              ))}
            </ul>
          </div>
          <div>
            <h2 className="text-xs font-semibold uppercase tracking-widest text-white/55">A tu manera</h2>
            <p className="mt-4 text-sm leading-relaxed text-white/65">
              Arma tu selección y continúa al checkout para revisar entrega y pago. También puedes consultar al negocio
              por WhatsApp.
            </p>
            <button
              onClick={() => setCartOpen(true)}
              className="mt-5 rounded-full border border-white/30 px-5 py-3 text-sm hover:bg-white/10"
            >
              Ver mi carrito ↗
            </button>
          </div>
        </div>
        <div className="mx-auto flex max-w-6xl flex-wrap justify-between gap-3 border-t border-white/15 px-5 py-5 text-xs text-white/50">
          <span>
            © {new Date().getFullYear()} {businessName}
          </span>
          <span>
            {isDemoCatalog ? 'Catálogo de demostración · Productos ilustrativos con IA' : 'Estilo para todos los días.'}
          </span>
        </div>
        <div className="mx-auto max-w-6xl px-5 pb-8">
          <StoreInformationLinks />
        </div>
      </footer>
      <CartModal open={cartOpen} catalogReady={status === 'ready'} onClose={() => setCartOpen(false)} />
    </>
  );
}
