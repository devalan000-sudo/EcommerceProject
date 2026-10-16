import { useEffect, useRef, useState } from 'react';

const steps = [
  {
    number: '01',
    title: 'Encuentra tu próximo favorito',
    description: 'Explora la colección y elige las prendas que van contigo.',
    icon: 'discover',
  },
  {
    number: '02',
    title: 'Arma un look muy tuyo',
    description: 'Agrega tus favoritos al carrito y revisa las cantidades y el total.',
    icon: 'cart',
  },
  {
    number: '03',
    title: 'Hazlo tuyo',
    description: 'Continúa con tu compra y elige cómo recibirla. Si necesitas ayuda, estamos en WhatsApp.',
    icon: 'message',
  },
];

function StepIcon({ type }) {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="size-6"
    >
      {type === 'discover' && (
        <>
          <path d="m12 3 2.5 6.5L21 12l-6.5 2.5L12 21l-2.5-6.5L3 12l6.5-2.5Z" />
          <path d="M20 3v4M18 5h4" />
        </>
      )}
      {type === 'cart' && (
        <>
          <path d="M2 3h3l3 12h11l3-9H6M8 15l-1 3h12" />
          <circle cx="9" cy="21" r="1" />
          <circle cx="18" cy="21" r="1" />
        </>
      )}
      {type === 'message' && (
        <>
          <path d="M21 11.5a9 9 0 0 1-13.3 7.9L3 21l1.6-4.7A9 9 0 1 1 21 11.5Z" />
          <path d="m8 11 3 3 5-5" />
        </>
      )}
    </svg>
  );
}

export default function ShoppingSteps() {
  const section = useRef(null);
  const [visible, setVisible] = useState(false);
  useEffect(() => {
    if (!('IntersectionObserver' in window)) {
      setVisible(true);
      return;
    }
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setVisible(true);
          observer.disconnect();
        }
      },
      { threshold: 0.12 },
    );
    observer.observe(section.current);
    return () => observer.disconnect();
  }, []);

  return (
    <section ref={section} aria-labelledby="shopping-steps-title" className="border-b border-brand/10 py-7 sm:py-9">
      <div className="mb-5 flex flex-wrap items-center justify-between gap-2">
        <h2 id="shopping-steps-title" className="text-lg font-semibold tracking-tight text-brand">
          De tu estilo a tu puerta.
        </h2>
        <span className="text-[10px] font-semibold uppercase tracking-[0.18em] text-stone-500">
          Así de fácil, en 3 pasos
        </span>
      </div>
      <ol className="grid gap-3 md:grid-cols-3">
        {steps.map((step, index) => (
          <li
            key={step.number}
            style={{ '--step-delay': `${index * 110}ms` }}
            className={`shopping-step group relative rounded-2xl border border-brand/10 bg-white p-5 sm:p-6 ${visible ? 'shopping-step-visible' : ''}`}
          >
            <div className="mb-4 flex items-center justify-between">
              <span className="shopping-step-icon flex size-12 items-center justify-center rounded-2xl bg-[#edf1e0] text-brand">
                <StepIcon type={step.icon} />
              </span>
              <span aria-hidden="true" className="text-3xl font-light tracking-tighter text-brand/20">
                {step.number}
              </span>
            </div>
            <h3 className="text-base font-semibold tracking-tight text-brand">{step.title}</h3>
            <p className="mt-2 text-sm leading-relaxed text-stone-500">{step.description}</p>
          </li>
        ))}
      </ol>
    </section>
  );
}
