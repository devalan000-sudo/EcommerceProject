import StoreImage from './StoreImage.jsx';
import { useStore } from '../../contexts/StoreContext/StoreContext.jsx';
import StoreNavigation from './StoreNavigation.jsx';
import { useCustomer } from '../../contexts/CustomerContext/CustomerContext.jsx';
import { useCart } from '../../contexts/CartContext/CartContext.jsx';

export default function StoreHeader({
  businessName,
  search,
  onSearchChange,
  onSearchSubmit,
  onOpenCart,
  onSelectCategory,
}) {
  const { settings } = useStore();
  const { session } = useCustomer();
  const { count } = useCart();
  return (
    <header className="sticky top-0 z-10 border-b border-brand/10 bg-paper/95 backdrop-blur">
      <div className="mx-auto grid max-w-6xl grid-cols-[minmax(0,1fr)_auto] items-center gap-x-5 gap-y-3 px-5 py-4 md:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_minmax(0,1fr)] md:gap-x-8 md:py-5">
        <a href="/" className="flex min-w-0 items-center gap-2 text-xl font-bold tracking-tight text-brand">
          <StoreImage src={settings.logoUrl} alt="" className="size-9 shrink-0 object-contain sm:size-11" />
          <span className="min-w-0 break-words">{businessName}</span>
        </a>

        <form
          role="search"
          onSubmit={(event) => {
            event.preventDefault();
            onSearchSubmit?.();
          }}
          className="col-span-2 row-start-2 min-w-0 md:col-span-1 md:col-start-2 md:row-start-1"
        >
          <label htmlFor="catalog-search" className="sr-only">
            Buscar productos
          </label>
          <div className="relative">
            <svg
              aria-hidden="true"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.7"
              strokeLinecap="round"
              className="pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2 text-stone-500"
            >
              <circle cx="10.5" cy="10.5" r="6.5" />
              <path d="m16 16 4 4" />
            </svg>
            <input
              id="catalog-search"
              type="search"
              value={search}
              onChange={(event) => onSearchChange(event.target.value)}
              placeholder="Buscar productos…"
              className="w-full rounded-full border border-brand/15 bg-white py-3 pr-4 pl-12 text-sm text-brand shadow-sm transition-colors placeholder:text-stone-400 hover:border-brand/30"
            />
          </div>
        </form>

        <div className="col-start-2 row-start-1 flex items-center justify-self-end gap-1 md:col-start-3">
          <a
            href="/account"
            className="flex min-h-11 items-center gap-1 rounded-full px-2 py-2 text-xs font-semibold hover:bg-brand/5 sm:text-sm"
          >
            <svg
              aria-hidden="true"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.7"
              className="size-5"
            >
              <circle cx="12" cy="8" r="4" />
              <path d="M4 21v-2a8 8 0 0 1 16 0v2" />
            </svg>
            {session ? 'Mi perfil' : 'Mi cuenta'}
          </a>
          <button
            type="button"
            onClick={onOpenCart}
            aria-haspopup="dialog"
            aria-label={`Carrito, ${count} ${count === 1 ? 'artículo' : 'artículos'}`}
            className="col-start-2 row-start-1 flex min-h-11 items-center justify-self-end gap-2 rounded-full px-3 py-2 text-sm font-semibold text-brand transition-colors hover:bg-brand/5 md:col-start-3"
          >
            <span>Carrito</span>
            <span className="relative inline-flex shrink-0">
              <svg
                aria-hidden="true"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.7"
                strokeLinecap="round"
                strokeLinejoin="round"
                className="size-6 shrink-0"
              >
                <path d="M2 3h3l3 12h11l3-9H6" />
                <path d="M8 15 7 18h12" />
                <circle cx="9" cy="21" r="1" />
                <circle cx="18" cy="21" r="1" />
              </svg>
              {count > 0 && (
                <span
                  aria-hidden="true"
                  className="absolute -right-2 -top-2 flex h-[18px] min-w-[18px] items-center justify-center rounded-full bg-red-600 px-1 text-[10px] font-bold leading-none tabular-nums text-white ring-2 ring-white"
                >
                  {count > 99 ? '99+' : count}
                </span>
              )}
            </span>
          </button>
        </div>
      </div>
      <StoreNavigation onSelectCategory={onSelectCategory} />
    </header>
  );
}
