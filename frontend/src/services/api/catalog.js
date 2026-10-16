import { api } from './client.js';
import { catalogRequest } from '../../utils/catalogRequest/catalogRequest.js';

// Lectura acotada; un fallo temporal admite sólo un reintento.
export function getCatalog(path, config = {}) {
  return catalogRequest(() => api.get(path, { ...config, timeout: 15000 }), config.signal);
}
