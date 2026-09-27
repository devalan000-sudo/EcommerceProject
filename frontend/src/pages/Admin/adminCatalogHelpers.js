export const emptyProduct = { name: '', description: '', price: '', imageUrl: '', categoryId: '', isActive: false };
export const emptyCategory = { name: '', description: '' };

export function newCatalogDraft(type) {
  return { ...(type === 'products' ? emptyProduct : emptyCategory) };
}

export function productIsPublishable(product) {
  return Boolean(
    product?.id &&
      product.isActive &&
      product.variants?.some((variant) => variant.isActive && variant.size?.trim() && variant.color?.trim()),
  );
}
