import test from 'node:test';
import assert from 'node:assert/strict';
import { themeVariables } from './storeTheme.js';

test('theme accepts hex and falls back for missing or invalid colors', () => {
  assert.equal(themeVariables({ primaryColor: '#123456' })['--color-brand'], '#123456');
  const fallback = themeVariables({ primaryColor: 'url(example)', accentColor: '' });
  assert.equal(fallback['--color-brand'], '#143f35');
  assert.equal(fallback['--color-paper'], '#f7f7f2');
  assert.equal(fallback['--color-accent'], '#d9e3b4');
});
