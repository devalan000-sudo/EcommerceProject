import test from 'node:test';
import assert from 'node:assert/strict';
import { emptyAddress, validateAddress, deliveryFromAddress } from './addresses.js';

test('la dirección se copia al pedido conservando el correo y sin modificar la libreta', () => {
  const saved = {
    firstName: 'Ana',
    lastName: 'Prueba',
    phone: null,
    formattedAddress: 'Calle Uno 123, CP 01234, México',
  };
  const delivery = deliveryFromAddress(saved, 'cuenta@example.test');
  assert.deepEqual(delivery, {
    firstName: 'Ana',
    lastName: 'Prueba',
    phone: '',
    email: 'cuenta@example.test',
    address: saved.formattedAddress,
    shippingAddress: { ...emptyAddress },
  });
  saved.formattedAddress = 'Otra dirección';
  assert.equal(delivery.address, 'Calle Uno 123, CP 01234, México');
  delivery.firstName = 'Otro destinatario';
  assert.equal(saved.firstName, 'Ana');
});

test('valida campos separados, espacios, CP mexicano y números alfanuméricos', () => {
  const address = {
    ...emptyAddress,
    street: 'Reforma',
    exteriorNumber: '12B',
    neighborhood: 'Centro',
    postalCode: '01234',
    city: 'Ciudad',
    state: 'Estado',
  };
  assert.deepEqual(validateAddress(address), {});
  assert.deepEqual(validateAddress({ ...address, exteriorNumber: 'S/N' }), {});
  assert.ok(validateAddress({ ...address, neighborhood: '   ' }).neighborhood);
  for (const postalCode of ['1234', '123456', '12A34'])
    assert.ok(validateAddress({ ...address, postalCode }).postalCode);
  assert.equal(address.postalCode, '01234');
});
