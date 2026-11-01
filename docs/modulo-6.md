# Módulo 6: seguridad y contraseñas

Ruta `/Seguridad`, exclusiva Admin, enlazada desde el dashboard. La aplicación permanece apagada. No se cambiaron contraseñas de usuarios reales ni se enviaron correos reales durante las pruebas.

## Funciones

`/Seguridad/Cambiar` exige la contraseña actual, nueva y confirmación. Usa ChangePasswordAsync de Identity sobre la cuenta autenticada: no acepta identificadores de otras cuentas. Los errores no vuelven a rellenar contraseñas en el HTML. Al cambiarla se renueva la cookie propia. SecurityStampValidator valida en cada petición autenticada, de modo que otras cookies anteriores se invalidan al volver a acceder.

`/Seguridad` permite consultar roles propios, roles existentes y número de cuentas asignadas mediante consultas EF. Explica los permisos implementados. No permite promover cuentas ni editar roles. Los controles reales siguen en los controladores, no en la visibilidad de botones.

`/Seguridad/Recuperar` y `/Seguridad/Restablecer` son accesibles sin sesión y usan tokens Data Protection de Identity. Solicitar enlace devuelve el mismo resultado para cuenta existente, inexistente o no confirmada. Solo se envía a correo confirmado. El token se codifica como base64url y se incluye en el enlace; caduca en una hora, queda ligado a cuenta/propósito y no vuelve a funcionar tras restablecer. No se revela ni recupera la contraseña anterior.

Cinco solicitudes POST por IP y hora compartidas entre solicitar/restablecer, sin cola; exceso HTTP 429. Las pantallas usan antiforgery, respuestas no-store y Referrer-Policy no-referrer. No se usa Request.Host para generar enlaces: la base pública proviene de configuración validada. No se registran contraseñas, enlaces o tokens en mensajes de aplicación. No activar logs de cuerpos HTTP ni registrar URLs completas de recuperación en proxies.

El enlace del login apunta a la recuperación unificada. Las rutas antiguas ForgotPassword y ResetPassword de Identity redirigen al flujo unificado, evitando otro mecanismo de envío sin límite. Las demás pantallas existentes de Identity se conservan.

## SMTP verificado con Mailtrap Sandbox

El usuario configuró Mailtrap Sandbox mediante User Secrets. Se verificaron TLS, autenticación y aceptación SMTP de un correo de recuperación real dirigido a prueba-recuperacion@example.test. También se verificó que el token asociado a la cuenta temporal funciona una vez y se rechaza al reutilizarlo. No se modificó app.db ni contraseñas reales. El mensaje de prueba está en el Sandbox con asunto Recuperar acceso a SegundaOportunidad; su enlace pertenece a una base efímera y no sirve para restablecer una cuenta de la página real. La recepción visual en la interfaz de Mailtrap queda a confirmar por el usuario. Sandbox captura los mensajes: no entrega al correo real del destinatario.

La comprobación explícita `dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --smtp-live` envía un mensaje y solo admite sandbox.smtp.mailtrap.io. La suite normal continúa usando correo simulado y no envía mensajes.

Configurar personalmente en User Secrets (desde la raíz) con los valores que proporcione el servidor SMTP:

```powershell
dotnet user-secrets set "RecoveryEmail:Host" "<servidor SMTP>" --project SegundaOportunidad
dotnet user-secrets set "RecoveryEmail:Port" "587" --project SegundaOportunidad
dotnet user-secrets set "RecoveryEmail:Username" "<usuario SMTP>" --project SegundaOportunidad
dotnet user-secrets set "RecoveryEmail:Password" "<credencial SMTP>" --project SegundaOportunidad
dotnet user-secrets set "RecoveryEmail:From" "<remitente autorizado>" --project SegundaOportunidad
dotnet user-secrets set "RecoveryEmail:PublicBaseUrl" "http://localhost:5194" --project SegundaOportunidad
```

Producción: variables `RecoveryEmail__Host`, `RecoveryEmail__Port`, `RecoveryEmail__Username`, `RecoveryEmail__Password`, `RecoveryEmail__From` y `RecoveryEmail__PublicBaseUrl`. La URL debe ser HTTPS; HTTP solo se acepta para loopback en Development. SMTP usa STARTTLS y timeout de 15 segundos; no se rebaja TLS. Confirmar puerto y modo con el proveedor: no está implementado SMTP de TLS implícito en puerto 465. El mismo remitente se registra como IEmailSender de Identity para confirmaciones de registros nuevos. Sin configuración conserva el comportamiento previo de no enviar esos mensajes.

No guardar credenciales en el repositorio ni compartirlas en el chat. En despliegue conservar de forma segura las claves Data Protection entre reinicios/instancias para que los tokens sigan siendo verificables. El limitador actual es por instancia; no es distribuido.

## Pruebas

```powershell
dotnet build SegundaOportunidad.slnx
dotnet run --project SegundaOportunidad.Checks --no-build
dotnet run --project SegundaOportunidad.Checks --no-build -- --verify-db SegundaOportunidad/app.db
```

SecurityChecks utiliza SQLite temporal, Identity, cookies reales y buzón simulado. Verifica permisos, antiforgery, contraseña actual incorrecta, cambio propio, persistencia, renovación de cookie, respuesta uniforme, recuperación HTTP, token de otra cuenta/manipulado/reutilizado/caducado, invalidación de sesión y rate limiting. Se vuelve a ejecutar la suite de los módulos previos. No equivale a comprobar entrega SMTP; esa prueba y la revisión visual quedan pendientes.

No se añadieron modelos persistentes, tablas ni migraciones. Archivos nuevos: SeguridadViewModels, RecoveryEmailService/opciones/registro de servicios, SeguridadController, cinco vistas en Views/Seguridad, redirecciones ForgotPassword y ResetPassword en Identity, SecurityChecks. Modificados: Program principal, enlace del login, dashboard, RealIdentityChecks y arnés Checks. Este último ahora captura fallos de comprobación, devuelve salida 1 y dispone recursos sin terminar por excepción administrada no controlada.

Compilación y suite aprobadas; persisten advertencias NuGet existentes. Sin commits, push, merges ni PR. No se implementaron cambios adicionales de negocio.
