export const templateSizes = ['S', 'M', 'L'];
export const popularColors = ['NEGRO', 'BLANCO', 'AZUL', 'GRIS'];
export const sizeLabel = (value) =>
  ({
    S: 'Chica',
    CH: 'Chica',
    CHICA: 'Chica',
    M: 'Mediana',
    MEDIANA: 'Mediana',
    L: 'Grande',
    G: 'Grande',
    GRANDE: 'Grande',
  })[value?.toUpperCase()] || value;
export const colorHex = (value) =>
  ({
    NEGRO: '#171717',
    BLANCO: '#ffffff',
    AZUL: '#2563eb',
    GRIS: '#737373',
    ROJO: '#dc2626',
    VERDE: '#15803d',
    BEIGE: '#d6c4a5',
    'AZUL MARINO': '#172554',
    VINO: '#701a2e',
    CAMEL: '#b99464',
    TERRACOTA: '#bb5a3f',
  })[value?.toUpperCase()] || '#e7e5e4';
export function availableVariant(variants, color, size) {
  return variants.find((v) => v.isActive && v.color === color && v.size === size && v.availableStock > 0);
}
export function imageForColor(product, color) {
  return product.variants?.find((v) => v.isActive && v.color === color && v.imageUrl)?.imageUrl || null;
}
