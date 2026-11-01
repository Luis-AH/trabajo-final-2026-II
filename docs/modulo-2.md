# Módulo 2: administración de publicaciones

Abrir `/AdminPublicaciones` con rol Admin o usar el enlace del dashboard `/Admin`. La tabla pagina de veinte en veinte y permite buscar, filtrar por categoría y estado, y revisar cada artículo con su propietario, descripción, fotografía e historial.

Desde el detalle elegir Ocultar, Suspender publicación, Eliminar lógicamente o Restaurar. Todas las acciones requieren motivo de 5 a 1000 caracteres, confirmación y antiforgery. Las decisiones registran administrador, fecha UTC y estados anterior/nuevo en una transacción de SaveChanges. Un token de concurrencia rechaza decisiones sobre versiones obsoletas. Una publicación eliminada solo admite restauración como siguiente paso.

Suspender publicación es un estado manual sin vencimiento automático; las suspensiones temporales de usuarios corresponden al Módulo 3. Restaurar conserva IsActive. No se borran artículos, relaciones ni intercambios físicamente.

El filtro global EF excluye publicaciones moderadas de consultas públicas, detalle y selección para intercambios. Las consultas administrativas omiten ese filtro explícitamente. El historial personal de intercambios conserva sus referencias a artículos moderados. Aceptar y completar comprueban también disponibilidad. El dashboard incluye conteos de moderación; disponibilidad y moderación son dimensiones independientes.

Algolia aporta identificadores y SQLite valida existencia, estados y categoría antes de paginar. Se consultan las páginas que Algolia permite recuperar; sigue aplicando el límite de resultados de la configuración del índice. Ante error se informa y se busca localmente en título/descripción. No se envían decisiones administrativas al índice: la visibilidad se controla en SQLite incluso si el índice está desactualizado. No se comprobó conectividad real con Algolia en esta fase.

## Base de datos

Se añadió migración EF ModeracionPublicaciones y una fábrica de contexto para ejecutar herramientas sin iniciar integraciones externas. El arranque usa MigrateAsync.

La base local app.db fue respaldada y actualizada. Para otra base anterior creada mediante EnsureCreated, detener la aplicación y ejecutar desde la raíz:

```powershell
dotnet run --project SegundaOportunidad.Checks -- --upgrade-db SegundaOportunidad/app.db
```

El comando crea un respaldo SQLite, compara tablas, columnas, claves e índices con el esquema inicial y solo registra las migraciones iniciales si es compatible. No compara orden de columnas ni defaults históricos: EnsureCreated y las migraciones iniciales generan diferencias conocidas en esos puntos. Si detecta otras diferencias, se detiene. Después aplica las migraciones pendientes. Con historial existente se usan directamente las migraciones EF. No ejecutar esta utilidad con la aplicación usando la base.

Los respaldos `*.backup-*.sqlite` quedan excluidos de Git. No subir bases ni respaldos que contengan información de usuarios.

## Pruebas

```powershell
dotnet build SegundaOportunidad.slnx
dotnet run --project SegundaOportunidad.Checks --no-build
```

Las pruebas usan SQLite temporal, migraciones, transiciones, auditoría, restauración, conflictos, permisos HTTP, Razor y antiforgery sin servicios externos. Prueba manual pendiente: entrar como Admin, ocultar un artículo con motivo, comprobar que desaparezca del catálogo público y que su URL no sea accesible, revisar historial y restaurarlo. Probar búsquedas con Algolia real, filtros y navegación móvil.

Archivos: modelo Articulo; nuevo ModeracionPublicacion; ApplicationDbContext y migración/snapshot; factory; seeder; servicio ModeracionPublicacionService; controlador AdminPublicacionesController; dos vistas administrativas; AlgoliaService; guardas de IntercambiosController; historial en PerfilController; estadísticas/modelo/vista de dashboard; proyecto Checks y utilidad de actualización; .gitignore. No se implementó gestión de usuarios ni se realizaron commits.
