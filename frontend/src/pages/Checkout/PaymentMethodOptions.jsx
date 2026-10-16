export default function PaymentMethodOptions({
  method,
  onMethodChange,
  stripeEnabled,
  storeLoading,
  storeError,
  whatsAppPhone,
}) {
  return (
    <>
      <label className="flex gap-3 rounded-xl border border-brand bg-brand/5 p-4 font-semibold">
        <input type="radio" name="method" checked={method === 'Card'} onChange={() => onMethodChange('Card')} />
        Tarjeta · Stripe de prueba
      </label>
      <label className="my-4 flex gap-2 text-sm text-stone-500">
        <input
          type="radio"
          name="method"
          disabled={storeLoading || storeError || !whatsAppPhone}
          checked={method === 'WhatsApp'}
          onChange={() => onMethodChange('WhatsApp')}
        />
        Prefiero coordinar mi pedido por WhatsApp
      </label>
      {method === 'Card' ? (
        <>
          <p className="mb-4 text-sm text-stone-500">
            Pruebas sin cargos reales. Al abrir Stripe reservamos tus artículos durante 15 minutos. Usa únicamente
            tarjetas de prueba.
          </p>
          {!stripeEnabled && (
            <p role="status" className="mb-4 rounded-xl bg-amber-50 p-4 text-sm">
              El pago con tarjeta aún no está habilitado. Puedes guardar el pedido pendiente de pago o elegir WhatsApp
              para coordinarlo con el negocio.
            </p>
          )}
        </>
      ) : (
        <p className="mb-4 text-sm text-stone-500">
          Guardar el pedido no reserva existencias ni confirma un pago. Coordina ambos con el negocio por WhatsApp.
        </p>
      )}
    </>
  );
}
