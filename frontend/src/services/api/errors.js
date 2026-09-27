export function errorMessage(error) {
  if (error.response?.status === 401) return 'La sesión venció. Inicia sesión nuevamente.';
  if (error.response?.status === 429) return 'Demasiados intentos. Espera un minuto y vuelve a intentar.';
  const body = error.response?.data;
  return (
    body?.message ||
    (body?.errors && Object.values(body.errors).flat().join(' ')) ||
    'No se pudo completar la operación. Comprueba tu conexión e intenta nuevamente.'
  );
}
