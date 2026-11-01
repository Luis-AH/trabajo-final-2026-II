# Módulo 3: gestión de usuarios

Ruta `/AdminUsuarios`, accesible desde Administrar usuarios en `/Admin`. Requiere rol Admin. Tabla paginada de veinte usuarios con búsqueda por nombre/correo y filtro de suspensión de cuenta. Las cantidades de publicaciones e intercambios completados se calculan en SQL, incluyendo artículos moderados. El perfil tiene historial paginado de decisiones, estado actual y formularios con confirmación y antiforgery.

Acciones disponibles: advertencia, suspensión de creación de publicaciones, suspensión de cuenta, reactivación de cuenta y reactivación de publicaciones. Las suspensiones admiten entre 1 y 8760 horas. Las demás acciones no admiten duración. Todas requieren motivo de 5 a 1000 caracteres. El estado y la entrada de historial se guardan juntos. El token de concurrencia rechaza decisiones sobre una versión anterior del usuario.

Un administrador no puede suspenderse a sí mismo, ni mediante solicitudes directas. Una suspensión vigente debe levantarse antes de volver a suspender la misma dimensión. Advertir no bloquea la cuenta: es una anotación administrativa, sin envío de notificación externa en esta fase. Reactivar cuenta no borra el historial ni levanta una suspensión independiente de publicaciones.

## Restricciones en el servidor

SuspensionSignInManager impide nuevos inicios de sesión de cuentas suspendidas. SuspensionMiddleware consulta el estado persistido en cada petición autenticada: bloquea sesiones ya abiertas con HTTP 403, permite cerrar sesión en `/Identity/Account/Logout` y libera el acceso al vencer el plazo. La suspensión de publicaciones bloquea GET y POST de `/Articulos/Create`; conserva artículos existentes y otras funciones. La suspensión de cuenta restringe toda petición autenticada salvo logout. No se publican sanciones automáticamente con RabbitMQ ni se usa IA para decidirlas.

Las cuentas sin suspensión no representan usuarios activos recientemente. El dashboard muestra totales globales de cuentas suspendidas y sin suspensión. RegisteredAtUtc se registra para nuevos usuarios del formulario de registro; las cuentas anteriores conservan null y se muestran como Sin registro histórico. Reportes recibidos aparece No disponible hasta el Módulo 4.

## Migración y pruebas

Migración EF GestionUsuariosSanciones: columnas nuevas en AspNetUsers y tabla SancionesUsuarios con claves restringidas e índice por usuario/fecha. Se respaldó y migró app.db local con la utilidad del Módulo 2; no se eliminaron datos. Otras instalaciones con historial EF pueden aplicar `dotnet ef database update --project SegundaOportunidad`; confirmar la ruta de conexión antes. El arranque aplica migraciones.

Desde la raíz:

```powershell
dotnet build SegundaOportunidad.slnx
dotnet run --project SegundaOportunidad.Checks --no-build
```

Las comprobaciones cubren migraciones sobre SQLite vacío, vistas Razor, permisos, antiforgery, autosuspensión, duración, reactivación inválida, conflictos, expiración, historial y bloqueo de sesiones abiertas. Usan SQLite en memoria y autenticación HTTP simulada; verifican además CanSignInAsync del SignInManager personalizado.

Prueba manual pendiente: iniciar como Admin, abrir Usuarios, seleccionar otro usuario y advertirlo. Suspender creación de publicaciones por una hora y comprobar con su sesión abierta que no puede publicar. Reactivar publicaciones y verificar acceso. Suspender la cuenta, comprobar que no inicia sesión y reactivarla como Admin. Revisar historial, filtros y diseño móvil. No usar la cuenta del propio administrador como objetivo de suspensión.

Archivos nuevos: AdminUsuariosController, AdminUsuariosViewModel, SancionUsuario, SancionUsuarioService, SuspensionMiddleware, SuspensionSignInManager, Views/AdminUsuarios/Index y Detalle, migración/designer, UserChecks y este documento. Modificados: ApplicationUser, ApplicationDbContext/snapshot, Program, registro Identity, estadísticas y vista del dashboard, DataSeeder y arnés Checks. Persisten advertencias NuGet previas. No se implementó Módulo 4 ni se realizaron commits/push/PR.
