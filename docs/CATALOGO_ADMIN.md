# Catálogo conectado al administrador

El 12 de septiembre de 2026 se importaron 32 productos de `frontend/src/data/demoCatalog.js` y sus cinco categorías a `EcommerceBoutique` en `.\SQLEXPRESS`. Se conservó General. Los productos siguen siendo ejemplos con precios ilustrativos e imágenes generadas con IA.

La configuración local de desarrollo establece `VITE_DEMO_CATALOG=false` en `frontend/.env.development.local`. Reinicia Vite después de cambiar esta configuración. La portada, `/comprar`, las fichas y `/admin` consultan la misma API. El catálogo se vuelve a consultar al cargar una vista; no hay actualizaciones en tiempo real entre pestañas. Recarga la tienda después de guardar cambios administrativos.

El administrador permite crear, editar, eliminar y activar/desactivar productos, con miniaturas en el listado. Los IDs de la base son independientes de los IDs demo. El carrito real está separado del carrito demo; las visitas reales comienzan en cero.

Las imágenes importadas apuntan a `http://localhost:5173/images/products/{id-demo}.png`. Ese origen es local: al desplegar se deben actualizar las URLs al dominio público de imágenes. Los archivos permanecen en `frontend/public/images/products`. Cambiar la URL en el CRUD cambia la imagen mostrada.

## Importación explícita y repetible

Desde la raíz, `node frontend/scripts/import-demo-catalog.mjs` verifica que existan las imágenes. Con `--apply` importa usando sqlcmd y autenticación de Windows dentro de una transacción. No sobrescribe registros: omite productos por nombre y reutiliza categorías por nombre. Si se renombra un producto y se vuelve a importar, el nombre demo original puede volver a crearse; no es un sincronizador permanente.

El script admite `CATALOG_SQL_SERVER`, `CATALOG_SQL_DATABASE` y `CATALOG_IMAGE_ORIGIN` para otros destinos. No contiene credenciales y no se ejecuta durante el arranque.

Al usar el catálogo de la API, WhatsApp usa el teléfono del negocio configurado y deja de marcar el mensaje como simulación. La importación no configura ese teléfono ni convierte los productos de muestra en inventario verificado.
