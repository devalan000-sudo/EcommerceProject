import { productImageUrl } from '../../utils/productImages/productImages.js';
import { useEffect, useState } from 'react';

export default function ProductGallery({ product }) {
  const images = [...new Set([product.imageUrl, ...(product.imageUrls || [])].filter(Boolean))];
  const [selected, setSelected] = useState(images[0]);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    setFailed(false);
  }, [selected]);
  return (
    <div>
      <div className="flex aspect-[4/5] items-center justify-center overflow-hidden rounded-3xl border border-stone-200/70 bg-white">
        {failed ? (
          <p className="p-6 text-stone-500">Imagen no disponible</p>
        ) : (
          <img
            src={productImageUrl(selected, 1024)}
            alt={`${product.name}, fotografía ${images.indexOf(selected) + 1}`}
            className="h-full w-full object-contain"
            fetchPriority="high"
            onError={() => setFailed(true)}
          />
        )}
      </div>
      <div className="mt-4 grid grid-cols-4 gap-3" aria-label="Fotografías del producto">
        {images.map((url, index) => (
          <button
            key={url}
            onClick={() => setSelected(url)}
            aria-label={`Ver fotografía ${index + 1}`}
            aria-pressed={selected === url}
            className={`aspect-square overflow-hidden rounded-xl border-2 bg-white ${selected === url ? 'border-brand' : 'border-transparent'}`}
          >
            <img src={productImageUrl(url)} alt="" className="h-full w-full object-contain" loading="lazy" />
          </button>
        ))}
        {Array.from({ length: Math.max(0, 4 - images.length) }, (_, index) => (
          <div
            key={`empty-${index}`}
            className="flex aspect-square flex-col items-center justify-center gap-1 rounded-xl border border-dashed border-stone-300 p-2 text-center text-stone-400"
          >
            <span aria-hidden="true" className="text-2xl">
              +
            </span>
            <span className="text-[10px] sm:text-xs">Sin fotografía</span>
          </div>
        ))}
      </div>
      {product.imageNote && <p className="mt-3 text-xs text-stone-500">{product.imageNote}</p>}
      {product.imageSource && (
        <a
          href={product.imageSource}
          target="_blank"
          rel="noopener noreferrer"
          className="mt-3 inline-block text-xs text-stone-500 underline"
        >
          Foto: {product.imageAuthor} / Unsplash
        </a>
      )}
    </div>
  );
}
