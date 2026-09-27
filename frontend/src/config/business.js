export const businessName = import.meta.env.VITE_BUSINESS_NAME || 'Ecommerce-Boutique';

// Mantiene la presentación actual hasta que el administrador guarde su tienda.
export const defaultStoreSettings = {
  name: businessName,
  whatsAppPhone: String(import.meta.env.VITE_BUSINESS_PHONE || '').replace(/[\s()+-]/g, ''),
  primaryColor: '#143f35',
  backgroundColor: '#f7f7f2',
  accentColor: '#d9e3b4',
  faviconUrl: '',
  instagramUrl: '',
  facebookUrl: '',
  tikTokUrl: '',
  contactEmail: '',
  contactPhone: '',
  businessAddress: '',
  businessHours: '',
  shippingPolicy: '',
  returnsPolicy: '',
  logoUrl: '',
  heroImageUrl: '',
  announcement: 'Estilo urbano · Tu próxima compra empieza aquí',
  heroTitle: 'Tu estilo.\nTus reglas.',
  heroDescription:
    'Playeras, gorras, tenis y las capas que hacen tu look. Encuentra tus próximos favoritos y arma tu pedido.',
  footerDescription:
    'Esenciales urbanos. Combinaciones sin límites. Tu próxima historia empieza con lo que llevas puesto.',
  version: '00000000-0000-0000-0000-000000000000',
};
