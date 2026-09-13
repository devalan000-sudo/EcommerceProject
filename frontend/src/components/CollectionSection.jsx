import { useState } from 'react';

function CollectionTile({ category, product, onSelect }) {
  const [failed, setFailed] = useState(false);
  return <button onClick={() => onSelect(String(category.id))} className="group relative min-h-52 overflow-hidden rounded-2xl border border-stone-200/70 bg-white text-left text-brand sm:min-h-64">
    {!failed && product && <img src={product.imageUrl} alt="" loading="lazy" className="absolute inset-0 h-full w-full object-contain px-3 pt-3 pb-20 transition-transform duration-500 motion-safe:group-hover:scale-105" onError={() => setFailed(true)} />}
    <div className="absolute inset-x-0 bottom-0 flex items-end justify-between gap-2 bg-white/95 p-4 sm:p-5"><div><span className="text-[10px] uppercase tracking-[0.2em] text-stone-500">Explorar</span><h3 className="mt-1 text-lg font-semibold sm:text-xl">{category.name}</h3></div><span aria-hidden="true" className="flex size-8 shrink-0 items-center justify-center rounded-full border border-brand/20">↗</span></div>
  </button>;
}

export default function CollectionSection({ categories, products, onSelect }) {
  const collections = categories.filter(category => products.some(p => p.isActive && p.categoryId === category.id));
  if (!collections.length) return null;
  return <section className="py-9 sm:py-12" aria-labelledby="collections-title">
    <div className="mb-5 flex flex-wrap items-end justify-between gap-3"><div><p className="text-[10px] font-semibold uppercase tracking-[0.2em] text-stone-500">Elige tu siguiente look</p><h2 id="collections-title" className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">Tu estilo empieza aquí.</h2></div><a href="#catalog" className="py-2 text-sm underline underline-offset-4">Ver todo el catálogo ↗</a></div>
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">{collections.map(category => <CollectionTile key={category.id} category={category} product={products.find(p => p.isActive && p.categoryId === category.id)} onSelect={onSelect} />)}</div>
  </section>;
}
