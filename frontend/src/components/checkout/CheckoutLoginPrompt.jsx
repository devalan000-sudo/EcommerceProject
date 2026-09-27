export default function CheckoutLoginPrompt({ loginUrl }) {
  return (
    <section className="rounded-2xl border border-stone-200 bg-white p-6">
      <p>Inicia sesión o crea una cuenta para registrar tu pedido. Tu carrito se conserva.</p>
      <a href={loginUrl} className="mt-4 inline-block rounded-xl bg-brand px-5 py-3 text-white">
        Entrar a mi cuenta
      </a>
    </section>
  );
}
