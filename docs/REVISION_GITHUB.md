# Revisión antes de publicar en GitHub

Fecha: 13 de septiembre de 2026.

Se revisaron los archivos de código, configuración y documentación candidatos a Git, sin imprimir valores privados. No se encontraron tokens de proveedores, JWT reales ni claves privadas en los archivos examinados. Las contraseñas y claves literales de los tests son datos ficticios usados con bases en memoria. Los ejemplos del README utilizan placeholders, no credenciales operativas.

## Cambios realizados

- Se ampliaron las exclusiones de `.gitignore`: variables locales, estado de Visual Studio, configuraciones por entorno, secretos, certificados, archivos de publicación, bases locales, respaldos, logs y resultados de pruebas.
- Se reemplazó una ruta personal de la documentación por un comando portable.
- Se comprobó con Git que las rutas locales sensibles quedan excluidas. Se utilizó un repositorio temporal dentro de `.tools`, que también está excluido; no se creó un repositorio en la raíz ni se publicó nada.

`frontend/.env.example` contiene ejemplos públicos y un teléfono vacío. `frontend/.env.development` se conserva únicamente como configuración compartida del modo demo. No deben añadirse secretos a ninguno. Las variables `VITE_*` son públicas: pueden incorporarse al JavaScript que recibe el navegador.

`appsettings.json` contiene configuración general sin clave JWT ni cadena de conexión. La API lee sus valores privados desde la configuración externa; los secretos de usuario de .NET se guardan fuera del proyecto. No se copiaron ni enumeraron esos secretos. El identificador UserSecretsId del csproj identifica el almacén; no es una credencial.

Las fotos del catálogo son archivos de muestra para la aplicación, no fotos personales. Los nombres de instancia local y localhost documentados no son contraseñas ni exponen por sí mismos SQL Server a Internet.

## Antes del primer commit

Después de inicializar el repositorio, revisar:

```powershell
git status --short
git diff --cached --name-only
git diff --cached
```

Estos comandos deben revisarse localmente; no compartir su salida si contiene valores privados. No usar `git add -f` para incluir archivos excluidos ni subir la carpeta completa como ZIP: las exclusiones solo afectan a Git.

## Alcance y límites

No había historial Git en la raíz al realizar la revisión. Por lo tanto, no se auditó historial ni contenido remoto. `.gitignore` no elimina secretos previamente publicados o ya rastreados. Si se descubre alguno publicado, debe revocarse o rotarse y retirarse también del historial correspondiente.

Esta revisión estática por patrones y configuración reduce el riesgo de exposición, pero no garantiza ausencia absoluta de secretos ni equivale a una auditoría de seguridad del servidor. Los cambios posteriores al análisis deben volver a revisarse. Postmark y Cloudinary deberán configurarse exclusivamente en secretos del backend cuando se integren.
