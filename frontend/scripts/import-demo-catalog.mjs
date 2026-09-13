import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { demoCategories, demoProducts } from '../src/data/demoCatalog.js';

// Importación local explícita. No se ejecuta al iniciar la tienda ni sobrescribe productos.
const apply = process.argv.includes('--apply');
const origin = new URL(process.env.CATALOG_IMAGE_ORIGIN || 'http://localhost:5173');
if (!['http:', 'https:'].includes(origin.protocol)) throw new Error('El origen de imágenes debe ser HTTP o HTTPS.');
const sqlString = value => `N'${String(value).replaceAll("'", "''")}'`;
for (const product of demoProducts) {
  if (!existsSync(new URL(`../public${product.imageUrl}`, import.meta.url))) {
    throw new Error(`Falta la imagen del producto ${product.id}.`);
  }
}
const statements = [
  'SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;',
  'DECLARE @categoriesAdded int = 0, @productsAdded int = 0;',
];
for (const category of demoCategories) {
  statements.push(`IF NOT EXISTS (SELECT 1 FROM dbo.Categories WITH (UPDLOCK, HOLDLOCK) WHERE Name = ${sqlString(category.name)})
  BEGIN
    INSERT dbo.Categories (Name, Description) VALUES (${sqlString(category.name)}, ${sqlString(category.description)});
    SET @categoriesAdded += 1;
  END;`);
}
for (const product of demoProducts) {
  // Se buscan categorías por nombre: sus IDs de SQL Server no son los IDs demo.
  statements.push(`IF NOT EXISTS (SELECT 1 FROM dbo.Products WITH (UPDLOCK, HOLDLOCK) WHERE Name = ${sqlString(product.name)})
  BEGIN
    INSERT dbo.Products (Name, Description, Price, ImageUrl, IsActive, CategoryId, ViewCount)
    VALUES (${sqlString(product.name)}, ${sqlString(product.description)}, ${product.price},
      ${sqlString(new URL(product.imageUrl, origin).href)}, 1,
      (SELECT TOP (1) Id FROM dbo.Categories WHERE Name = ${sqlString(product.categoryName)} ORDER BY Id), 0);
    SET @productsAdded += 1;
  END;`);
}
statements.push('COMMIT; SELECT @categoriesAdded AS CategoriesAdded, @productsAdded AS ProductsAdded;');
if (!apply) {
  console.log(`Listos ${demoProducts.length} productos y ${demoCategories.length} categorías. Imágenes verificadas. Usa --apply para importar.`);
} else {
  const result = spawnSync('sqlcmd', [
    '-S', process.env.CATALOG_SQL_SERVER || '.\\SQLEXPRESS',
    '-d', process.env.CATALOG_SQL_DATABASE || 'EcommerceBoutique',
    '-E', '-C', '-b', '-l', '10', '-f', '65001', '-W',
  ], { input: statements.join('\n'), encoding: 'utf8', windowsHide: true });
  if (result.error) throw result.error;
  process.stdout.write(result.stdout || '');
  process.stderr.write(result.stderr || '');
  process.exitCode = result.status ?? 1;
}
