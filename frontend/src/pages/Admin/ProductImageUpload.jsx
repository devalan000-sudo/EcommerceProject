import { useRef, useState } from 'react';
import { api } from '../../services/api/client.js';
import { errorMessage } from '../../services/api/errors.js';
import StoreImage from '../../components/layout/StoreImage.jsx';
import { uploadProductImage } from '../../services/admin/adminService.js';

export default function ProductImageUpload({
  session,
  imageUrl,
  disabled,
  onUploaded,
  onBusy,
  label = 'Subir imagen del producto',
}) {
  const lock = useRef(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  async function upload(event) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file || lock.current) return;
    setError('');
    setNotice('');
    if (
      !['image/jpeg', 'image/png', 'image/webp'].includes(file.type) ||
      file.size <= 0 ||
      file.size > 5 * 1024 * 1024
    ) {
      setError('Selecciona una imagen JPG, PNG o WebP de hasta 5 MB.');
      return;
    }
    lock.current = true;
    setBusy(true);
    onBusy(true);
    try {
      const data = new FormData();
      data.append('file', file);
      const result = await uploadProductImage(api, session, data);
      onUploaded(result.data.imageUrl);
      setNotice('Imagen subida. Guarda los cambios para publicarla.');
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      lock.current = false;
      setBusy(false);
      onBusy(false);
    }
  }
  return (
    <div className="rounded-xl border border-stone-200 p-4">
      <label className="block text-sm font-medium">
        {label}
        <input
          type="file"
          accept="image/jpeg,image/png,image/webp"
          disabled={disabled || busy}
          onChange={upload}
          className="mt-3 block w-full text-sm file:mr-4 file:rounded-lg file:border-0 file:bg-brand file:px-4 file:py-2 file:text-white disabled:opacity-40"
        />
      </label>
      <p className="mt-2 text-xs text-stone-500">
        JPG, PNG o WebP, máximo 5 MB. La imagen anterior se conserva hasta guardar los cambios.
      </p>
      {busy && (
        <p role="status" className="mt-3 text-sm">
          Subiendo imagen…
        </p>
      )}
      {error && (
        <p role="alert" className="mt-3 text-sm text-red-700">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="mt-3 text-sm text-emerald-800">
          {notice}
        </p>
      )}
      <StoreImage
        src={imageUrl}
        alt="Vista previa del producto"
        className="mt-4 h-40 w-40 rounded-lg bg-white object-contain"
      />
    </div>
  );
}
