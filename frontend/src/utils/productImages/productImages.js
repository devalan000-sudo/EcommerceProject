// Only the bundled sample photos have pre-generated derivatives. Keep other URLs intact.
export function productImageUrl(source, width = 640) {
  if (!source) return source;
  try {
    const url = new URL(source, 'http://catalog.local');
    const local = ['catalog.local', 'localhost', '127.0.0.1'].includes(url.hostname);
    const match = url.pathname.match(/^\/images\/products\/(10(?:0[1-9]|[12][0-9]|3[0-2]))\.png$/);
    if (local && match && !url.search && !url.hash)
      return `/images/products/optimized/${match[1]}-${width > 640 ? 1024 : 640}.jpg`;
    // Versioned public Cloudinary uploads only: do not alter signed/custom URLs.
    if (
      url.protocol === 'https:' &&
      url.hostname === 'res.cloudinary.com' &&
      !url.search &&
      !url.hash &&
      /\/image\/upload\/v\d+\//.test(url.pathname)
    ) {
      url.pathname = url.pathname.replace(
        '/image/upload/',
        `/image/upload/f_auto,q_auto,c_limit,w_${width > 640 ? 1024 : 640}/`,
      );
      return url.href;
    }
  } catch {
    /* Preserve the existing image error handling. */
  }
  return source;
}
