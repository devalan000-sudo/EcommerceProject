export default function CheckoutHeader({ businessName }) {
  return (
    <header className="border-b border-stone-200 bg-white px-5 py-5">
      <a href="/" className="text-xl font-bold text-brand">
        {businessName}
      </a>
    </header>
  );
}
