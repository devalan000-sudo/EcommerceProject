import { useStore } from '../../contexts/StoreContext/StoreContext.jsx';

export function StoreInformationLinks() {
  const { settings } = useStore();
  return (
    <div className="flex flex-wrap gap-5 text-sm">
      <a href="/informacion" className="underline">
        Contacto e información
      </a>
      {settings.shippingPolicy && (
        <a href="/informacion#envios" className="underline">
          Política de envíos
        </a>
      )}
      {settings.returnsPolicy && (
        <a href="/informacion#devoluciones" className="underline">
          Cambios y devoluciones
        </a>
      )}
      {['Instagram', 'Facebook', 'TikTok'].map(
        (name) =>
          settings[`${name[0].toLowerCase()}${name.slice(1)}Url`] && (
            <a
              key={name}
              href={settings[`${name[0].toLowerCase()}${name.slice(1)}Url`]}
              target="_blank"
              rel="noopener noreferrer"
              className="underline"
            >
              {name}
            </a>
          ),
      )}
    </div>
  );
}

export default function StoreInformation() {
  const { settings, loading, error } = useStore();
  const sections = [
    ['direccion', 'Dirección del negocio', settings.businessAddress],
    ['horarios', 'Horarios de atención', settings.businessHours],
    ['envios', 'Política de envíos', settings.shippingPolicy],
    ['devoluciones', 'Cambios y devoluciones', settings.returnsPolicy],
  ];
  return (
    <main className="mx-auto max-w-3xl px-5 py-10">
      <a href="/" className="text-brand underline">
        Volver a {settings.name}
      </a>
      <h1 className="mt-7 text-3xl font-bold">Contacto e información</h1>
      {loading ? (
        <p role="status" className="mt-6">
          Cargando información…
        </p>
      ) : error ? (
        <p role="alert" className="mt-6">
          No pudimos cargar la información. Recarga la página para intentarlo de nuevo.
        </p>
      ) : (
        <>
          <div className="my-6 space-y-3 break-words">
            {settings.contactEmail && (
              <p>
                Correo:{' '}
                <a className="underline" href={`mailto:${settings.contactEmail}`}>
                  {settings.contactEmail}
                </a>
              </p>
            )}
            {settings.contactPhone && <p>Teléfono: {settings.contactPhone}</p>}
            {!settings.contactEmail && !settings.contactPhone && !sections.some(([, , value]) => value) && (
              <p>La tienda aún no ha publicado sus datos de contacto y políticas.</p>
            )}
          </div>
          {sections.map(
            ([id, title, content]) =>
              content && (
                <section key={id} id={id} className="my-6 scroll-mt-6 rounded-2xl border border-stone-200 bg-white p-6">
                  <h2 className="text-xl font-semibold">{title}</h2>
                  <p className="mt-4 whitespace-pre-wrap break-words leading-relaxed">{content}</p>
                </section>
              ),
          )}
          <StoreInformationLinks />
        </>
      )}
    </main>
  );
}
