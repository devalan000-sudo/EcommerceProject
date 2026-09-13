import test from 'node:test';
import assert from 'node:assert/strict';
import { normalize } from './search.js';

test('la búsqueda mantiene equivalencia de mayúsculas y acentos sin alterar espacios', () => {
  assert.equal(normalize('EDICIÓN Café'), normalize('edicion cafe'));
  assert.equal(normalize('  Tenis  '), '  tenis  ');
  assert.equal(normalize(''), '');
});
