import { useEffect, useState } from 'react';
import { productImageUrl } from '../../utils/productImages/productImages.js';

export default function CartProductImage({ item }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [item.imageUrl]);
  return (
    <div className="flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-stone-200 bg-white sm:size-20">
      {item.imageUrl && !failed ? (
        <img
          src={productImageUrl(item.imageUrl)}
          alt={item.name}
          width="80"
          height="80"
          decoding="async"
          onError={() => setFailed(true)}
          className="h-full w-full object-contain p-1"
        />
      ) : (
        <span className="px-1 text-center text-[10px] leading-tight text-stone-500">Imagen no disponible</span>
      )}
    </div>
  );
}
