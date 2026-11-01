# Dashboard administrativo

Ruta: `/Admin`. Iniciar sesión con el administrador existente después de reiniciar la aplicación. Si la sesión era anterior a la asignación del rol, cerrar sesión y entrar nuevamente.

El seeder crea el rol Admin y lo asigna al identificador de administrador ya previsto por el proyecto. Los demás usuarios no reciben ese rol. No se agrega un mecanismo de promoción pública.

No hay cambios de esquema: las tablas Identity de roles ya están definidas en las migraciones existentes. Se conserva EnsureCreated; su reconciliación con el historial de migraciones requiere una fase posterior.

## Datos y filtros

- Usuarios: total global, sin filtro temporal ni categoría.
- Publicaciones: creadas en el período UTC inclusive, con su estado actual activo/inactivo.
- Intercambios: creados en el período, con su estado actual; filtro por categoría del artículo solicitado.
- Series: publicaciones e intercambios creados por mes, incluyendo meses vacíos.
- Categorías: hasta diez con más publicaciones en el período.
- Suspensiones, eliminación lógica y reportes: no disponibles hasta implementar sus modelos y flujos. No se muestran ceros ficticios.

Chart.js 4.4.8 se carga desde jsDelivr. Si no está disponible, se pueden consultar las tablas desplegables de datos.

## Verificación

Desde la raíz del repositorio:

```powershell
dotnet build SegundaOportunidad.slnx
dotnet run --project SegundaOportunidad.Checks/SegundaOportunidad.Checks.csproj
dotnet run --project SegundaOportunidad/SegundaOportunidad.csproj
```

Las comprobaciones usan SQLite en memoria y un servidor HTTP local con autenticación simulada, exclusivamente en el proyecto Checks. No usan app.db ni Algolia, Redis o RabbitMQ. Cubren agregaciones, filtros, fechas, acceso por roles y renderizado Razor. No sustituyen la prueba de login real ni una inspección visual del navegador.

Prueba manual: entrar como Admin, abrir Administración, cambiar período/categoría, contrastar tablas y gráficas y visitar Verificaciones de identidad. Comprobar diseño en móvil. Entrar como usuario común y verificar que `/Admin` y `/Supervisor` den acceso denegado. Sin sesión deben solicitar login. Las acciones de verificación requieren token antiforgery.

Persisten las advertencias NU1901 de NuGet.Packaging y NuGet.Protocol 6.12.1. No se actualizaron dependencias en esta fase. No se implementaron módulos 2–6.
