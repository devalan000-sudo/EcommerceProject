import { productImageUrl } from '../../utils/productImages/productImages.js';
import { useEffect, useState } from 'react';

export default function StoreImage({ src, alt, className, fallback = null }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [src]);
  return src && !failed ? (
    <img
      src={productImageUrl(src)}
      alt={alt}
      loading="lazy"
      decoding="async"
      className={className}
      onError={() => setFailed(true)}
    />
  ) : (
    fallback
  );
}
