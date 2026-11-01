# Módulo 5: AdminGuard IA

Ruta `/AdminGuard`, exclusiva del rol Admin, enlazada desde el dashboard, detalle administrativo de publicación y detalle de reporte. La aplicación permanece apagada. Los análisis se muestran como texto codificado, sin ejecutar HTML ni Markdown generado por el modelo.

## Configuración real pendiente

Se eligió Groq con el usuario. Después de configurar la clave se verificó autenticación mediante el listado real de modelos y se guardó `qwen/qwen3.8-27b` en User Secrets. Una primera prueba devolvió HTTP 429; una consulta mínima posterior y la prueba visual real completaron correctamente. Se verificaron descarga segura de fotografía pública, SHA-256, respuesta multimodal y auditoría en SQLite temporal, sin modificar app.db. No se presentan resultados simulados en producción ni se reintenta automáticamente.

Desde la raíz, configurar personalmente sin compartir la clave en el chat ni subirla al repositorio:

```powershell
dotnet user-secrets set "AdminGuard:ApiKey" "<tu_clave>" --project SegundaOportunidad
dotnet user-secrets set "AdminGuard:Model" "<modelo_multimodal_habilitado_en_tu_cuenta>" --project SegundaOportunidad
```

En producción usar `AdminGuard__ApiKey` y `AdminGuard__Model`. Seleccionar un modelo de visión disponible en la cuenta, siguiendo https://console.groq.com/docs/vision y https://console.groq.com/docs/api-reference. No se fija un nombre de modelo que pueda retirarse. Se usa HTTPS POST a api.groq.com/openai/v1/chat/completions, Bearer, texto e image_url de tipo data URI y max_completion_tokens. Reiniciar después de configurar.

## Funciones

- Publicación: descarga la fotografía autorizada, calcula SHA-256 y envía bytes, título/descripción redactados, estado y conteo de reportes para comparar consistencia y sugerir verificaciones. Si falta imagen o no se puede descargar, registra el intento fallido sin fingir análisis visual.
- Duplicados: compara SHA-256 con análisis completados de otras publicaciones. Comprueba igualdad exacta de bytes, no similitud perceptual, recortes, autoría ni fraude. Solo cubre fotografías ya analizadas. Las referencias son históricas; los artículos pueden haber cambiado.
- Reporte: resumen del motivo, descripción redactada y antecedentes agregados por motivo/estado y tipo de decisión de sanción. No envía nombres, correos de Identity, DNI, contraseñas, hashes de contraseña ni tokens.
- Estadísticas/prioridades: preguntas sobre una instantánea global de totales y distribuciones por estado/motivo. No hay SQL generado ni acceso del modelo a bases, herramientas o endpoints de escritura. Las prioridades son sugerencias de revisión por cantidades/tipos, no estimación verificable de culpabilidad. Preguntas fuera de esos agregados carecen de datos para responder.

La redacción de texto libre elimina patrones comunes de correos, números largos y secretos etiquetados. Es heurística: el administrador debe evitar datos personales innecesarios en títulos, descripciones y preguntas. Las imágenes se envían a Groq al confirmar la consulta; no existe eliminación automática de texto visible en fotografías.

## Seguridad y límites

Autorización en servidor y antiforgery. Cada consulta registra administrador, tipo, objetivo, fecha UTC, pregunta redactada, modelo, hash de imagen cuando existe, resultado y estado de completitud. No se guardan credenciales ni bytes de fotografía. Historial de veinte análisis recientes del propio administrador. Solo la tabla de análisis recibe escrituras del flujo de IA; los datos de negocio son de lectura.

Diez intentos por administrador en una hora, incluyendo fallos, con contador persistido. Un semáforo limita a un análisis simultáneo por instancia; otras consultas deben reintentarse al terminar. Esta coordinación es de una sola instancia, no distribuida. HTTP Groq: 45 segundos y respuesta limitada a 128 KiB; imagen: 15 segundos y máximo 4 MiB. No se reintentan automáticamente llamadas que puedan consumir cuota.

Imágenes: HTTPS, puerto estándar, sin credenciales en URL y dominio exactamente incluido en AdminGuard:ImageHosts (por defecto solo images.unsplash.com). Sin redirecciones ni proxy. Resolución DNS y conexión a la IP validada excluyen loopback, redes privadas y metadatos. Solo firmas JPEG/PNG/WebP. Para otro almacenamiento, autorizar explícitamente un dominio público de confianza con la configuración, nunca hosts internos. El proveedor puede rechazar imágenes que superen sus propios límites de resolución; se informa como fallo.

Prompt del sistema trata publicaciones, reportes e imágenes como datos no confiables y pide separar observaciones, incertidumbres y verificaciones. No afirma falsedad ni fraude sin evidencia. El modelo no dispone de herramientas de sanción ni modifica contraseñas. Los enlaces de resultados llevan a formularios humanos ya existentes y no ejecutan acciones.

## Pruebas y archivos

```powershell
dotnet build SegundaOportunidad.slnx
dotnet run --project SegundaOportunidad.Checks --no-build
dotnet run --project SegundaOportunidad.Checks --no-build -- --verify-db SegundaOportunidad/app.db
```

GuardChecks usa proveedor/transporte/imagen simulados exclusivamente en SQLite temporal: autorización HTTP, antiforgery, codificación de salida, datos mínimos, redacción, igualdad de hash, resúmenes, auditoría de fallos, límites, URLs/SSRF y contrato multimodal sin llamadas externas. Se ejecuta la suite anterior. Pendiente: prueba real con clave/modelo, fotografía real y cuotas del proveedor, además de revisión visual en navegador. No se debe afirmar que el servicio externo está verificado antes de esa prueba.

Migración AdminGuardAnalisis agrega únicamente la tabla de auditoría e índices. La base local se respalda antes de aplicarla. Archivos nuevos: AnalisisAdminGuard/modelos de pantalla, AdminGuardClient/options/interfaz, AdminGuardImageReader, AdminGuardService/gate, AdminGuardController, Views/AdminGuard/Index y Resultado, migración/designer y GuardChecks. Modificados: DbContext/snapshot, Program, enlaces de dashboard/publicación/reporte, RealIdentityChecks. Sin dependencias NuGet nuevas, commits, push o PR. Persisten las advertencias NuGet previas.

## Corrección del formulario de consulta

La ruta GET /AdminGuard/Consultar ahora redirige al panel; POST sigue siendo la acción protegida con antiforgery. Se reemplazó la referencia de texto libre por un selector de publicaciones/reportes existentes, con el elemento del enlace preseleccionado. La referencia se valida antes de consumir una consulta del proveedor. Un objetivo inexistente o retirado devuelve un mensaje en el panel, no una página 404. Las respuestas de Groq sin texto se validan explícitamente, sin confundir una propiedad JSON ausente con una publicación inexistente.

GuardChecks cubre acceso directo GET, referencia inexistente, selector y envío HTTP de una publicación válida hasta su página de resultado con proveedor simulado. La suite completa pasó. No se hicieron nuevas llamadas a Groq para esta corrección.
