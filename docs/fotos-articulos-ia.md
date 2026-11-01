# Foto obligatoria, categorías y revisión de IA

Publicar artículo ahora requiere un archivo PNG o JPEG de hasta 4 MiB. El formulario multipart permite previsualizar la foto y valida tamaño/tipo en cliente; el servidor vuelve a comprobar presencia, tamaño, extensión y firmas de formato. No se confía en Content-Type ni en la ruta del archivo enviada. Los listados mantienen los enlaces antiguos para no romper publicaciones existentes.

La foto nueva se guarda con el artículo en una única operación EF (tabla FotosArticulos, relación uno a uno). Los bytes están separados del modelo de catálogo para no cargarlos en cada listado. ImageUrl reutiliza la ruta `/Articulos/Imagen/{id}`. No se guardan archivos ejecutables ni se usan nombres originales como rutas. El endpoint responde con MIME concreto, nosniff y sin caché; una foto de publicación moderada solo la puede consultar Admin.

Las categorías se amplían idempotentemente sin reemplazar IDs ni duplicar las existentes: ropa, muebles, electrónica, libros, deportes, hogar/decoración, electrodomésticos, computación, celulares, herramientas, juguetes, bebés/niños, música, arte/manualidades, jardín, cuidado personal, calzado/accesorios, mascotas, oficina, videojuegos y Otros... La opción Otros... aparece al final de la selección. Se conservan categorías adicionales preexistentes.

## AdminGuard

En Administrar publicaciones → Revisar se ve la fotografía adjunta y el acceso Analizar con AdminGuard. Para rutas locales, el lector de IA consulta la foto en SQLite y envía esos mismos bytes al proveedor; no intenta descargar localhost ni necesita permitir dominios adicionales. Conserva el lector seguro de URLs para las publicaciones antiguas.

El análisis compara imagen, título y descripción, registra SHA-256 para coincidencias exactas y devuelve observaciones para revisión humana. No acredita autenticidad, identidad, propiedad ni ausencia de estafa. No sanciona ni oculta automáticamente: el administrador decide con confirmación en los formularios existentes. El usuario mantiene el requisito previo de aprobación administrativa para publicar.

## Pruebas y estado

PublicationChecks ejecuta solicitudes HTTP multipart con cookies reales en SQLite temporal. Verifica foto obligatoria, archivo renombrado, límite de tamaño, categoría inválida, ausencia de inserciones inválidas, publicación válida, bytes persistidos, MIME, SHA-256 de IA y acceso restringido a fotos ocultas. El indexador Algolia se simula en ese host de prueba, evitando escrituras en el índice compartido. La suite anterior también pasa.

La prueba real `--guard-live` se actualizó para guardar la fotografía pública de ejemplo en SQLite temporal y analizarla desde la ruta local. En esta ejecución Groq devolvió HTTP 429: el contrato local pasó con proveedor simulado, pero la respuesta real de ese intento queda pendiente de cuota/disponibilidad. No se introdujeron fotos o artículos de prueba en app.db ni se cambiaron claves.

Se añade migración FotosArticulos. Aplicada con respaldo previo a la base local; el seeder añade categorías al arrancar sin recrear publicaciones. SQLite debe tener almacenamiento persistente en despliegue para conservar fotografías.

```powershell
dotnet build SegundaOportunidad.slnx
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll
# Solo cuando se disponga de cuota; consume una consulta real:
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --guard-live
```

Archivos: FotoArticulo, ArticuloPhotoService, DbContext/migración/snapshot, CategoryCatalog y DataSeeder, ArticulosController/Create.cshtml, lector AdminGuard, detalle administrativo, filtro de aprobación para imágenes públicas, orden de categorías, PublicationChecks y GuardLiveChecks. No se realizaron commits ni push.
