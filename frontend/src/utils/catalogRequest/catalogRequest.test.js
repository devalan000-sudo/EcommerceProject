import test from 'node:test';
import assert from 'node:assert/strict';
import { catalogRequest, catalogErrorMessage } from './catalogRequest.js';

test('reintenta una lectura que agotó su tiempo y devuelve el catálogo', async () => {
  let calls = 0;
  const response = await catalogRequest(async () => {
    if (++calls === 1) throw { code: 'ECONNABORTED' };
    return ['producto'];
  });
  assert.deepEqual(response, ['producto']);
  assert.equal(calls, 2);
});
test('limita a dos intentos incluso cuando la API sigue sin responder', async () => {
  let calls = 0;
  await assert.rejects(() =>
    catalogRequest(async () => {
      calls++;
      throw new Error('fallo', { cause: 'test' });
    }),
  );
  assert.equal(calls, 1);
  calls = 0;
  const error = Object.assign(new Error('timeout'), { code: 'ETIMEDOUT' });
  await assert.rejects(() =>
    catalogRequest(async () => {
      calls++;
      throw error;
    }),
  );
  assert.equal(calls, 2);
  assert.match(catalogErrorMessage(error), /tardando/);
});
test('no reintenta solicitudes canceladas ni errores de autorización', async () => {
  for (const [error, signal] of [
    [{ code: 'ERR_NETWORK' }, { aborted: true }],
    [{ response: { status: 401 } }, undefined],
  ]) {
    let calls = 0;
    await assert.rejects(() =>
      catalogRequest(async () => {
        calls++;
        throw Object.assign(new Error(), error);
      }, signal),
    );
    assert.equal(calls, 1);
  }
});
