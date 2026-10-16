export default function AdminMetric({ label, value, help }) {
  return (
    <article className="min-w-0 rounded-2xl border border-stone-200 bg-white p-5">
      <h3 className="text-sm text-stone-600">{label}</h3>
      <p className="mt-3 break-words text-2xl font-semibold tabular-nums text-brand">{value}</p>
      {help && <p className="mt-2 text-xs leading-relaxed text-stone-500">{help}</p>}
    </article>
  );
}
