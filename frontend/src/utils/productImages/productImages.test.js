import test from 'node:test';
import assert from 'node:assert/strict';
import { productImageUrl } from './productImages.js';

test('serves small sample photos without changing unknown or signed URLs', () => {
  assert.equal(
    productImageUrl('http://localhost:5173/images/products/1001.png'),
    '/images/products/optimized/1001-640.jpg',
  );
  assert.equal(productImageUrl('/images/products/1032.png', 1024), '/images/products/optimized/1032-1024.jpg');
  for (const url of [
    '/images/products/9999.png',
    'https://example.com/images/products/1001.png',
    'https://res.cloudinary.com/demo/image/upload/s--signature--/v1/photo.jpg',
  ])
    assert.equal(productImageUrl(url), url);
});
test('requests automatic format and quality for versioned public Cloudinary photos', () => {
  assert.equal(
    productImageUrl('https://res.cloudinary.com/demo/image/upload/v123/photo.jpg'),
    'https://res.cloudinary.com/demo/image/upload/f_auto,q_auto,c_limit,w_640/v123/photo.jpg',
  );
});
