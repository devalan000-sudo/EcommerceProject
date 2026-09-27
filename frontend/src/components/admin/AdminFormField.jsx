export default function AdminFormField({ label, children }) {
  return (
    <label className="block text-sm font-medium">
      {label}
      {children}
    </label>
  );
}
