import { useState } from 'react';

export default function AdminProductImage({ imageUrl, name }) {
  const [failed, setFailed] = useState(false);
  return (
    <div className="flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-stone-200 bg-white">
      {imageUrl && !failed ? (
        <img
          src={imageUrl}
          alt={name}
          loading="lazy"
          onError={() => setFailed(true)}
          className="h-full w-full object-contain p-1"
        />
      ) : (
        <span className="p-1 text-center text-[10px] text-stone-500">Sin imagen</span>
      )}
    </div>
  );
}
