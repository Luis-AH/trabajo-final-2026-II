# Aprobación obligatoria para participar

Una cuenta normal sin aprobación administrativa solo puede iniciar/cerrar sesión, consultar catálogo/detalles, su perfil/dashboard, gestionar recuperación de acceso y solicitar/consultar verificación. No puede publicar, abrir conversaciones, enviar mensajes, proponer/aceptar/rechazar/completar intercambios ni reportar. Las rutas MVC no previstas quedan restringidas para cuentas no aprobadas. Los Admin conservan sus funciones administrativas y de publicación.

El filtro global VerifiedAccessFilter consulta SQLite en cada solicitud MVC, sin confiar en botones, claims de verificación ni caché. GET de negocio restringido redirige a `/Verificacion`; POST devuelve 403. Las autorizaciones Admin y los controles de suspensión siguen aplicándose. No impide cambiar credenciales propias mediante Identity.

## Flujo

1. Registrar una cuenta normal (sin verificación automática).
2. Abrir `/Verificacion`, desde el aviso del dashboard/perfil. Introducir DNI de exactamente ocho dígitos y enviar mediante antiforgery.
3. Estado pendiente: puede seguir explorando, sin realizar operaciones de negocio.
4. Admin abre `/Supervisor` y aprueba o rechaza. La aprobación guarda fecha UTC y administrador responsable. No permite aprobar sin una solicitud con DNI válido.
5. Al aprobar, la sesión existente puede participar en su siguiente solicitud. Al rechazar/retirar aprobación, vuelve a quedar restringida y debe solicitarla nuevamente.

Es una revisión manual del dato declarado: no consulta RENIEC ni acredita automáticamente identidad. Esta barrera reduce acceso sin revisar; no garantiza ausencia de fraude o bots.

La migración AprobacionUsuarios agrega VerifiedAtUtc/VerifiedByAdminId y EstadosSistema. Las verificaciones antiguas sin responsable se invalidan para cuentas normales y requieren nueva solicitud; los Admin se conservan. Se elimina la verificación automática y la siembra de publicaciones de demostración del seeder. El endpoint antiguo Home/SubmitDni ya no escribe datos sin la nueva validación.

## Mensajería

Solo se muestran destinatarios aprobados y no se admiten envíos a cuentas sin aprobar o suspendidas. El consumidor valida ambos participantes antes de guardar/notificar. SendMessage exige antiforgery y la vista lo envía en el encabezado. El contenido se inserta como texto, sin innerHTML.

La limpieza guarda una fecha de corte `chat-reset` en EstadosSistema: el consumidor descarta mensajes anteriores aunque sigan pendientes en RabbitMQ. No se purgó una cola remota compartida con compañeros ni se borró el índice Algolia. Las búsquedas públicas vuelven a validar IDs contra SQLite y su filtro de moderación.

## Limpieza local autorizada

El usuario autorizó publicaciones, intercambios y mensajes. Antes de migrar/limpiar se creó un respaldo SQLite completo (ignorado por Git). Se retiraron lógicamente las seis publicaciones existentes, conservando relaciones, reportes y auditorías. Se eliminaron intercambios y mensajes locales; ambos tenían cero filas en ese momento. Se conservaron tres usuarios y sus roles/categorías. El catálogo queda vacío hasta que una cuenta aprobada o Admin publique. Las publicaciones eliminadas siguen disponibles para auditoría administrativa; no se borraron físicamente.

La utilidad explícita `--reset-content RUTA_DB` realiza esa limpieza transaccional y no se ejecuta al iniciar la aplicación. No usar en otra base sin autorización. Las consultas normales y nuevas publicaciones no eliminan contenido automáticamente.

## Verificación

```powershell
dotnet build SegundaOportunidad.slnx
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --verify-db SegundaOportunidad/app.db
```

VerificationChecks prueba con Identity y cookies reales: login sin verificar, catálogo y detalles, bloqueo de URLs/POST, DNI inválido, solicitud, aprobación Admin, disponibilidad inmediata en sesión abierta, rechazo posterior, fecha/responsable y mensajes anteriores al corte. La suite anterior también pasó. No se alteraron contraseñas reales ni se enviaron correos durante estas comprobaciones. No hay commits/push de este cambio.

Archivos: VerifiedAccessFilter, ParticipationRules, VerificacionController/vista, EstadoSistema, columnas de ApplicationUser, DbContext/migración/snapshot, configuración MVC, Supervisor/Home/Mensajes, ChatBackgroundService, seeder, avisos y controles en dashboard/perfil/detalle/layout/chat; ResetContent, VerificationChecks, ajustes de fixtures y verificador de base. Se conservaron los cambios visuales pendientes del turno anterior.
