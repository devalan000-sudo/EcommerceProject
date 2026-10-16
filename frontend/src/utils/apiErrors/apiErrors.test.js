import test from 'node:test';
import assert from 'node:assert/strict';
import { errorMessage } from '../../services/api/errors.js';

test('conserva prioridades y mensajes de autenticación, validación y conexión', () => {
  assert.equal(
    errorMessage({ response: { status: 401, data: { message: 'Otro' } } }),
    'La sesión venció. Inicia sesión nuevamente.',
  );
  assert.equal(
    errorMessage({ response: { status: 429 } }),
    'Demasiados intentos. Espera un minuto y vuelve a intentar.',
  );
  assert.equal(
    errorMessage({ response: { data: { message: 'Correo duplicado', errors: { Email: ['Inválido'] } } } }),
    'Correo duplicado',
  );
  assert.equal(
    errorMessage({ response: { data: { errors: { Email: ['Correo inválido'], Address: ['Dirección incompleta'] } } } }),
    'Correo inválido Dirección incompleta',
  );
  assert.equal(errorMessage({}), 'No se pudo completar la operación. Comprueba tu conexión e intenta nuevamente.');
});
