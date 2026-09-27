import { useEffect, useRef, useState } from 'react';

const sections = [
  { id: 'shipping', label: 'Envíos' },
  { id: 'shopping', label: 'Cómo comprar' },
];

export default function StoreNavigation({ onSelectCategory }) {
  const [open, setOpen] = useState(null);
  const container = useRef(null);
  const triggers = useRef({});

  useEffect(() => {
    if (!open) return;
    function outside(event) {
      if (!container.current?.contains(event.target)) setOpen(null);
    }
    function escape(event) {
      if (event.key === 'Escape') {
        setOpen(null);
        triggers.current[open]?.focus();
      }
    }
    document.addEventListener('pointerdown', outside);
    document.addEventListener('keydown', escape);
    return () => {
      document.removeEventListener('pointerdown', outside);
      document.removeEventListener('keydown', escape);
    };
  }, [open]);

  function select(id) {
    onSelectCategory(String(id));
    setOpen(null);
    document.getElementById('catalog')?.focus({ preventScroll: true });
    document.getElementById('catalog')?.scrollIntoView({ block: 'start', behavior: 'smooth' });
  }

  return (
    <nav
      aria-label="Navegación de la tienda"
      ref={container}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) setOpen(null);
      }}
      className="border-t border-brand/10 bg-white/80"
    >
      <div className="relative mx-auto max-w-6xl px-5">
        <div className="flex min-h-12 flex-wrap items-center justify-center gap-1 py-1 md:gap-5">
          <button
            type="button"
            onClick={() => select('')}
            className="shrink-0 rounded-lg px-3 py-2.5 text-sm font-semibold text-brand transition-colors hover:bg-brand/5"
          >
            Productos
          </button>
          <a
            href="/comprar"
            aria-current={window.location.pathname.replace(/\/+$/, '') === '/comprar' ? 'page' : undefined}
            className="shrink-0 rounded-lg px-3 py-2.5 text-sm font-semibold text-brand transition-colors hover:bg-brand/5 aria-[current=page]:bg-brand aria-[current=page]:text-white"
          >
            Comprar / Armar pedido
          </a>
          {sections.map((section) => (
            <button
              key={section.id}
              type="button"
              ref={(element) => {
                triggers.current[section.id] = element;
              }}
              id={`nav-${section.id}`}
              aria-expanded={open === section.id}
              aria-controls={`panel-${section.id}`}
              onClick={() => setOpen((current) => (current === section.id ? null : section.id))}
              className={`flex shrink-0 items-center gap-2 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors hover:bg-brand/5 ${open === section.id ? 'bg-brand/5 text-brand' : 'text-stone-600'}`}
            >
              {section.label}
              <svg
                aria-hidden="true"
                viewBox="0 0 16 16"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.5"
                className={`size-3.5 transition-transform ${open === section.id ? 'rotate-180' : ''}`}
              >
                <path d="m4 6 4 4 4-4" />
              </svg>
            </button>
          ))}
          <a href="/informacion" className="rounded-lg px-3 py-2.5 text-sm font-medium text-stone-600 hover:bg-brand/5">
            Contacto y políticas
          </a>
        </div>
        {sections.map((section) => (
          <section
            key={section.id}
            id={`panel-${section.id}`}
            aria-labelledby={`nav-${section.id}`}
            hidden={open !== section.id}
            className="absolute top-full right-5 left-5 z-20 mt-2 max-h-[55dvh] overflow-y-auto rounded-2xl border border-brand/10 bg-white p-5 shadow-xl md:right-auto md:left-1/2 md:w-[32rem] md:-translate-x-1/2 md:p-6"
          >
            {section.id === 'shipping' && (
              <>
                <h2 className="text-lg font-semibold text-brand">Elige cómo recibir tu pedido</h2>
                <p className="mt-3 text-sm leading-relaxed text-stone-600">
                  En el checkout puedes consultar las opciones de entrega disponibles y revisar su costo antes de
                  confirmar. Para una entrega especial, consulta al negocio por WhatsApp.
                </p>
                <p className="mt-3 rounded-xl bg-paper p-3 text-sm text-stone-600">
                  El carrito muestra el importe de los productos. Al seleccionar una entrega en el checkout verás el
                  total con envío.
                </p>
              </>
            )}
            {section.id === 'shopping' && (
              <>
                <h2 className="text-lg font-semibold text-brand">Tu pedido en tres pasos</h2>
                <ol className="mt-4 list-decimal space-y-3 pl-5 text-sm leading-relaxed text-stone-600">
                  <li>Explora el catálogo y agrega tus productos al carrito.</li>
                  <li>Elige talla y color, revisa las cantidades y pulsa “Continuar con mi compra”.</li>
                  <li>
                    Inicia sesión, selecciona la entrega y revisa el importe. Puedes pagar con tarjeta cuando esté
                    habilitada o coordinar tu pedido por WhatsApp.
                  </li>
                </ol>
              </>
            )}
          </section>
        ))}
      </div>
    </nav>
  );
}
