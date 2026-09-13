# Pruebas automatizadas

Resultado del 12 de septiembre de 2026: 22 casos frontend y 22 casos backend correctos, sin omitidos. Se añadieron 26 casos sobre los 18 anteriores. No se modificó código de producción ni se agregaron dependencias.

## Ejecutar

Desde la raíz del proyecto:

```powershell
npm.cmd --prefix frontend test
dotnet test backend/Catalog.Api.Tests -p:UseAppHost=false "-p:BaseOutputPath=$((Get-Location).Path)/.tools/refactor-bin/"
```

En otra computadora adapta BaseOutputPath a una carpeta absoluta local. Esa opción separa los binarios de prueba de los usados por Visual Studio. No hace falta iniciar la API ni el frontend. El proyecto .NET puede requerir restaurar sus paquetes la primera vez.

## Alcance

| Área | Casos cubiertos |
|---|---|
| Carrito | Recuperación de almacenamiento inválido, cantidades y límites, conservación de preferencias, actualización de precios, exclusión de inactivos y eliminación |
| WhatsApp | Codificación, igualdad entre vista previa y enlace, cantidades, total, caracteres especiales, simulación, detalles largos y rechazo de datos inválidos |
| Búsqueda y visitas | Acentos/mayúsculas, destacados, desempates, ventana temporal y almacenamiento bloqueado/corrupto |
| Errores HTTP | Mensajes de autenticación, límite de solicitudes, validación y conexión |
| DTOs .NET | Rango y precisión de precios, URL absoluta HTTP/HTTPS, dirección y teléfono opcional |
| JWT | Firma válida, audiencia, identidad, rol, expiración a una hora e identificador distinto por emisión |
| Servicios de catálogo | Crear, leer, editar y eliminar; visibilidad pública/admin; asociación de categoría; impedir borrado de categoría con producto inactivo |
| Clientes y autenticación | Registro, hash, normalización y unicidad de correo, login, edición de perfil y roles declarados en endpoints |

Las funciones JavaScript y las pruebas de DTO/JWT son unitarias. Las pruebas de servicios EF son de integración con SQLite en memoria: no usan EcommerceBoutique, secretos, credenciales reales ni servicios externos. Las claves JWT usadas en pruebas son valores ficticios exclusivos de los tests.

## Límites

No se mide un porcentaje de cobertura. La suite no sustituye pruebas del navegador, accesibilidad, middleware HTTP completo, entrega real de WhatsApp ni pruebas del proveedor SQL Server. En particular, los errores específicos de SqlException y las migraciones SQL Server no se verifican con SQLite. La comprobación de atributos de autorización no equivale a probar el pipeline HTTP.

Las pruebas están en `frontend/src/utils/*.test.js` y `backend/Catalog.Api.Tests/*.cs`. Los casos nuevos de backend se agrupan en `ValidationTests`, `TokenTests` y `CatalogServiceTests`.
