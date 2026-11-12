# Accesos de presentación desde los archivos

`appsettings.Demo.json` habilita las claves públicas del catálogo ficticio solo
en el entorno `Demo`. El Dockerfile ya selecciona ese entorno. No hace falta
importar credenciales en Render para esta modalidad.

Administración: `sofia.ramos@example.invalid`, clave `Admin123!`.
Las otras nueve cuentas ficticias conservan su correo y usan `Usuario123!`.

El inicializador crea cuentas nuevas o sincroniza las diez existentes mediante
Identity, conservando publicaciones y demás registros. Solo selecciona los IDs
del catálogo ficticio y valida sus correos. Las cuentas ordinarias no cambian.
En este modo las claves del archivo prevalecen sobre un CredentialsJson antiguo.

Son accesos públicos para una presentación, incluido el administrador. No usar
este entorno con datos reales. Production sigue rechazando la inicialización
de presentación. Para claves privadas, desactivar
`SampleData:UsePublicDemoPasswords` y configurar `SampleData:CredentialsJson`.

Los cambios locales deben llegar a la rama desplegada y generar una nueva
versión para que cambie la página alojada. No actualizan un despliegue existente
por sí solos.
