import { normalize } from '../utils/search.js';
import { businessName } from '../config/business.js';
import { useEffect, useState } from 'react';
import { api } from '../api/client.js';
import { isDemoCatalog } from '../data/catalogMode.js';
import { demoProducts, demoCategories } from '../data/demoCatalog.js';
import { useCart } from '../context/CartContext.jsx';
import { formatMXN } from '../utils/money.js';
import StoreHeader from './StoreHeader.jsx';
import ProductCard from './ProductCard.jsx';
import CartModal from './CartModal.jsx';

export default function OrderView() {
  const [products, setProducts] = useState([]);
  const [categories, setCategories] = useState([]);
  const [category, setCategory] = useState(() => new URLSearchParams(location.search).get('category') || '');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('loading');
  const [attempt, setAttempt] = useState(0);
  const [cartOpen, setCartOpen] = useState(false);
  const { syncProducts, count, total } = useCart();


  useEffect(() => {
    const controller = new AbortController();
    setStatus('loading');
    async function load() {
      try {
        const [p, c] = isDemoCatalog ? [demoProducts, demoCategories] : await Promise.all([
          api.get('/products', { signal: controller.signal }).then(r => r.data),
          api.get('/categories', { signal: controller.signal }).then(r => r.data),
        ]);
        if (controller.signal.aborted) return;
        setProducts(p.filter(item => item.isActive)); setCategories(c); syncProducts(p); setStatus('ready');
      } catch { if (!controller.signal.aborted) setStatus('error'); }
    }
    void load();
    return () => controller.abort();
  }, [attempt, syncProducts]);

  const selected = categories.find(c => String(c.id) === category) || categories[0];
  const categoryProducts = products.filter(p => p.categoryId === selected?.id);
  const visible = categoryProducts.filter(p => normalize(`${p.name} ${p.description}`).includes(normalize(search.trim())));
  function choose(id) {
    setCategory(String(id)); setSearch('');
    const url = new URL(location.href);
    if (id) url.searchParams.set('category', id); else url.searchParams.delete('category');
    window.history.replaceState(null, '', url);
  }

  return <>
    <StoreHeader businessName={businessName} search={search} onSearchChange={setSearch} onOpenCart={() => setCartOpen(true)} onSelectCategory={choose} />
    <main className="mx-auto max-w-6xl px-5 py-8 pb-36">
      <a href="/" className="text-sm text-stone-500 underline">← Volver al inicio</a>
      <section className="my-7 rounded-3xl bg-brand p-6 text-white sm:p-10">
        <p className="text-xs uppercase tracking-[0.2em] text-white/65">Una categoría a la vez</p>
        <h1 className="mt-3 text-3xl font-semibold tracking-tight sm:text-5xl">Arma tu próximo look.</h1>
        <p className="mt-4 max-w-xl text-sm leading-relaxed text-white/80">Explora las categorías, agrega tus favoritos y reúne todo en un mismo pedido.</p>
      </section>
      {isDemoCatalog && <p className="mb-6 text-xs text-stone-500">Catálogo de muestra · Imágenes generadas con IA y precios ilustrativos. No disponible para compra.</p>}
      {status === 'loading' && <p role="status">Cargando categorías y productos…</p>}
      {status === 'error' && <div role="alert" className="rounded-xl border border-red-200 bg-white p-6">No pudimos cargar el catálogo. <button className="underline" onClick={() => setAttempt(n => n + 1)}>Reintentar</button></div>}
      {status === 'ready' && <>
        <nav aria-label="Categorías para armar tu pedido" className="mb-8 flex flex-wrap gap-3">
          {categories.map(c => <button key={c.id} aria-pressed={selected?.id === c.id} onClick={() => choose(c.id)} className={`min-h-12 rounded-xl border px-5 py-3 text-sm font-semibold transition-colors ${selected?.id === c.id ? 'border-brand bg-brand text-white' : 'border-stone-200 bg-white text-brand hover:border-brand'}`}>
            {c.name} <span className="ml-2 opacity-60">{products.filter(p => p.categoryId === c.id).length}</span>
          </button>)}
        </nav>
        <section id="catalog" tabIndex={-1} className="scroll-mt-64" aria-labelledby="order-category-title">
          <h2 id="order-category-title" className="text-3xl font-semibold">{selected?.name || 'Próximamente'}</h2>
          <p className="mt-2 text-sm text-stone-500">{selected?.description}</p>
          <p role="status" className="my-5 text-sm text-stone-500">{visible.length} productos en esta categoría{search.trim() ? ` para “${search.trim()}”` : ''}</p>
          {search && <button onClick={() => setSearch('')} className="mb-5 text-sm underline">Limpiar búsqueda</button>}
          {visible.length ? <div className="grid grid-cols-1 gap-x-5 gap-y-9 min-[400px]:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">{visible.map(p => <ProductCard key={p.id} product={p} />)}</div> : <p className="rounded-xl border border-dashed border-stone-300 p-8 text-stone-500">{search.trim() ? 'No hay coincidencias en esta categoría. Prueba otra búsqueda o cambia de categoría.' : 'Todavía no hay productos disponibles en esta categoría.'}</p>}
        </section>
      </>}
    </main>
    <div className="fixed inset-x-0 bottom-0 z-10 border-t border-stone-200 bg-white p-4 shadow-lg">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3">
        <div><p className="text-xs text-stone-500">{count} artículos en tu pedido</p><p className="text-lg font-semibold">{formatMXN(total)} <span className="text-xs font-normal text-stone-500">· Envío por confirmar</span></p></div>
        <button onClick={() => setCartOpen(true)} className="min-h-12 rounded-full bg-brand px-6 py-3 text-sm font-semibold text-white hover:bg-emerald-900">Revisar mi pedido →</button>
      </div>
    </div>
    <CartModal open={cartOpen} onClose={() => setCartOpen(false)} catalogReady={status === 'ready'} />
  </>;
}
