# Nombre completo y cierre de sesión

El filtro global de aprobación también se ejecutaba sobre Razor Pages de Identity y rechazaba con HTTP 403 el logout de una cuenta sin verificar. Ahora permite las páginas del área Identity para gestionar acceso y credenciales; las acciones de publicación, conversación, intercambio y reporte siguen bloqueadas sin aprobación.

Se reemplazó el dropdown de cuenta de escritorio por una tarjeta con Mi perfil y botón directo Cerrar sesión, mediante POST con antiforgery. La barra lateral admite desplazamiento cuando el menú es más alto que la pantalla. El menú móvil conserva el botón de salir. El enlace de la barra general también usa POST y texto en español.

Registro y solicitud de verificación piden nombres y apellidos completos, máximo 150 caracteres. Se valida en servidor que haya al menos dos componentes con letras, se rechazan controles y se normalizan espacios. Se reutiliza ApplicationUser.FullName: no hay tablas ni migraciones nuevas. El nombre se guarda al registrar o enviar la solicitud; no se inventan nombres para cuentas antiguas.

RegistrationChecks verifica el registro incompleto, persistencia y normalización del nombre, ausencia de aprobación automática, presencia del botón y logout efectivo con cookies reales de una cuenta no verificada. Los correos de ese registro se simulan; no se enviaron mensajes ni se modificaron cuentas reales. También se ejecutó la suite previa.

Archivos: Register.cshtml y su PageModel; NombreCompletoAttribute; VerificacionController y su vista; formulario del dashboard; _DashboardLayout, _LoginPartial y site.css; VerifiedAccessFilter; RegistrationChecks y fixtures de verificación/Identity. Sin commits ni push.
