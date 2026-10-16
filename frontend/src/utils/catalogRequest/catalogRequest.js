// Sólo para lecturas: nunca reintentar pagos, registros ni otras escrituras.
export async function catalogRequest(request, signal) {
  try {
    return await request();
  } catch (error) {
    const transient =
      ['ECONNABORTED', 'ETIMEDOUT', 'ERR_NETWORK'].includes(error.code) ||
      [502, 503, 504].includes(error.response?.status);
    if (signal?.aborted || !transient) throw error;
    return request();
  }
}

export function catalogErrorMessage(error) {
  if (['ECONNABORTED', 'ETIMEDOUT'].includes(error.code))
    return 'El catálogo está tardando en responder. Espera unos segundos y vuelve a intentarlo.';
  if (error.response?.status >= 500)
    return 'La tienda no pudo consultar el catálogo. Intenta nuevamente en unos segundos.';
  return 'No pudimos cargar el catálogo. Comprueba que la API esté en ejecución e intenta nuevamente.';
}
