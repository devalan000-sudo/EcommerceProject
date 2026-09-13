import { useEffect, useRef, useState } from 'react';
import { useCart } from '../context/CartContext.jsx';
import { formatMXN } from '../utils/money.js';

export default function ProductCard({ product }) {
  const { add } = useCart();
  const [failedImage, setFailedImage] = useState(false);
  const [added, setAdded] = useState(false);
  const timer = useRef(null);
  useEffect(() => { setFailedImage(false); }, [product.imageUrl]);
  useEffect(() => () => clearTimeout(timer.current), []);
  function addProduct() {
    add(product); setAdded(true); clearTimeout(timer.current);
    timer.current = setTimeout(() => setAdded(false), 1600);
  }
  return <article className="group flex min-w-0 flex-col">
    <a href={`/products/${product.id}`} aria-label={`Ver producto: ${product.name}`} className="relative flex aspect-[4/5] items-center justify-center overflow-hidden rounded-2xl border border-stone-200/70 bg-white">
      {failedImage ? <span className="p-4 text-center text-sm text-stone-500">Imagen no disponible</span> :
        <img className="h-full w-full object-contain transition-transform duration-500 motion-safe:group-hover:scale-105" src={product.imageUrl} alt={product.name} loading="lazy" decoding="async" onError={() => setFailedImage(true)} />}
      <span className="absolute top-3 left-3 rounded-full bg-white/95 px-3 py-1.5 text-[10px] font-semibold uppercase tracking-widest text-brand">{product.categoryName}</span>
    </a>
    <div className="flex flex-1 flex-col px-1 pt-4">
      <div className="flex flex-wrap items-baseline justify-between gap-x-2 gap-y-1"><h3 className="text-base font-semibold tracking-tight sm:text-lg"><a href={`/products/${product.id}`} className="hover:underline">{product.name}</a></h3><p className="text-sm font-semibold">{formatMXN(product.price)} <span className="text-[10px] font-normal text-stone-500">MXN</span></p></div>
      <p className="mt-2 line-clamp-2 text-sm leading-relaxed text-stone-500">{product.description}</p>
      <a href={`/products/${product.id}`} className="mt-4 flex min-h-11 items-center justify-center rounded-xl bg-brand px-4 py-3 text-sm font-semibold text-white hover:bg-emerald-900">Ver producto ↗</a>
      <button onClick={addProduct} className={`mt-4 flex min-h-11 w-full items-center justify-between gap-2 rounded-xl border px-4 py-3 text-sm font-semibold transition-colors ${added ? 'border-brand bg-brand text-white' : 'border-brand/20 bg-white hover:border-brand hover:bg-brand hover:text-white'}`}>
        <span aria-live="polite">{added ? 'Agregado al carrito' : 'Agregar al carrito'}</span><span aria-hidden="true" className="text-lg">{added ? '✓' : '+'}</span>
      </button>
      {product.imageSource && <a href={product.imageSource} target="_blank" rel="noopener noreferrer" className="mt-2 text-[10px] text-stone-500 underline decoration-stone-300 underline-offset-2">Foto: {product.imageAuthor} / Unsplash</a>}
    </div>
  </article>;
}


