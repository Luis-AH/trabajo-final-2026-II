> ACTUALIZACION 11/11/2026: Google Maps retirado por solicitud del usuario para evitar esa dependencia de facturacion. Selector, mapas, puntos de encuentro, endpoints, servicios Google, JS y cartografia eliminados. No configurar GoogleMaps__BrowserApiKey ni GoogleMaps__ServerApiKey. Compilacion, regresion y pruebas HTTP sin Maps aprobadas. Base app.db intacta; columnas/migracion se conservan por compatibilidad, sin borrar datos. El resto de este documento registra tambien el avance historico anterior a esa decision.

# Avance final — SegundaOportunidad

Fecha: 11/11/2026 (America/Lima). Rama: `feature/reciclaje-ronaldo`. Trabajo exclusivamente local, sin commits, push ni merge.

## Estado al retomar

| Fase | Estado comprobado |
|---|---|
| Scikit-learn | Implementado y probado: TF-IDF, coseno, entrenamiento offline, API y tarjetas; métricas sintéticas documentadas |
| Datos DEMO | 10 cuentas, 48 artículos, 6 intercambios en base aislada; idempotencia comprobada |
| Maps Perú | Implementado con validación territorial y permisos; llamadas reales pendientes por falta de claves/billing |
| Interfaz | Rediseño implementado y vistas HTTP probadas; revisión visual de navegador pendiente |
| Integraciones | Gemini 200, SMTP Sandbox correcto, consulta Algolia y conexión RabbitMQ correctas; nueva prueba Groq multimodal aprobada; PieSocket 403 pendiente |
| Render free | Configuración efímera y arranques Demo probados; sin Docker ni despliegue real |
| Regresión | Build y suite aprobados; advertencias NU1901 existentes |

## Trabajo de continuación

- Completado: seed rechaza usuarios normales, publicaciones/intercambios ajenos, mensajes o reportes; conserva administración inicial. Transacción evita altas parciales. Pruebas de base vacía, idempotencia, usuario real, publicación real, corpus inválido y Production aprobadas.
- Completado: validación de artefactos ML ausentes, JSON malformado, campos nulos, vocabulario vacío/índices duplicados y pesos incompatibles. Paridad numérica conservada; API con `Cache-Control: no-store`.
- Completado: pruebas de encuentros con formularios obsoletos (409), conservación del acuerdo vigente y nueva confirmación al cambiar propuesta.
- Completado: `scripts/check-frontend.mjs` prueba texto malicioso, enlaces locales, imágenes seguras, límite de tarjetas, vacío, 503, HTML inesperado, conexión y timeout. Se añadió cancelación de 15 segundos y estado de carga accesible en recomendaciones.
- Completado: contrato JavaScript Maps simulado: Autocomplete PE, Places solo centra y clic explícito aporta coordenadas, limpiar selección y error de clave. No equivale a Maps real.
- Completado: CLI DEMO rechaza app.db (también variantes de mayúsculas en Windows) y rutas externas a .demo/temporales antes de migrar.
- Completado: HTTP real sobre copia temporal: seed con CSRF, repetición sin mostrar contraseñas, rechazo 409 de mezcla de datos, edición segura y coordenadas inválidas.
- Completado: entrenamiento repetido en otro directorio produjo SHA256 idéntico `64CB0D74D8CFAE97B5D179271B84A6BEF92CF4551DDCD57C5A9C3083803689CA`, con Precision@5=0.9000, nDCG@5=0.9117275471 y cobertura=0.675. Evaluación sintética, no usuarios reales.
- Completado: `scripts/audit-release.ps1` verifica 11 recursos, ausencia de archivos privados/locales conocidos y hash del modelo. Publicación: 118 MiB de archivos; no es RAM. Docker no está instalado en PATH ni en ubicación estándar.
- Completado: build final exacto y suite completa aprobados; registro local `.demo/checks-autonomos.log` con 303 comprobaciones OK (no 303 casos de un framework unitario).
- Completado: nueva prueba real Groq con `qwen/qwen3.8-27b`, imagen, SHA256 y auditoría SQLite temporal. El 429 previo no se repitió. Sin cambios de credenciales ni código de AdminGuard.
- Completado: dos nuevos arranques Demo con SQLite temporal, API ML disponible, preparación requiere sesión y no se activa el seed al iniciar. Data Protection usa el directorio temporal configurado.
- Completado: revisión final de 308 archivos versionables contra 17 valores privados locales, sin mostrarlos y sin coincidencias. Inventario actualizado después de la revisión. app.db conserva 1204224 bytes y escritura del 06/11/2026 13:15:51.

## Errores y límites encontrados

- Corregidos: riesgo de mezcla/altas parciales DEMO, artefactos ML nulos que podían causar excepciones, caché de recomendaciones personales y espera indefinida en frontend.
- Corregido durante implementación: CS1061 en prueba nueva por falta de `using Microsoft.Extensions.Configuration`; compilación final sin errores.
- Persisten advertencias NU1901 de NuGet.Packaging/Protocol 6.12.1. No se cambiaron dependencias existentes para ocultarlas.
- Maps real: faltan claves y facturación. Distrito sin catálogo ADM3 validado. No se activó facturación.
- PieSocket: última prueba members=403, health=200. Ruta/autenticación V3 contrastadas con documentación oficial; requiere revisar credenciales/permisos del mismo proyecto/cluster en su panel, sin compartir secretos.
- SMTP real a usuarios y ciclo RabbitMQ/PieSocket completo aún no comprobados. Sandbox y conexión al broker no demuestran entrega extremo a extremo.
- Render/Docker: sin despliegue real ni build Linux. SQLite, fotos y Data Protection serán efímeros en free por decisión del usuario.
- Revisión visual desktop/móvil bloqueada por verificación de URL de Computer Use. No se usó otra tecnología para eludirla.

## Restricciones que se mantienen

- `SegundaOportunidad/app.db` no se modifica. La migración de ubicaciones se prueba solo en bases de ensayo.
- No se cambian claves ni se habilita facturación. No se escriben servicios compartidos.
- No se repiten implementaciones ya correctas ni se crea un servicio Python adicional en Render.
- Computer Use quedó bloqueado al verificar la URL de Edge; no se certifica revisión visual ni se intenta eludir el bloqueo.

## Documentación e inventario

- `docs/ml-mapas-demo-render.md`: arquitectura, métricas, comandos, tabla exacta de variables y pasos de despliegue.
- `docs/archivos-ml-mapas-demo.md`: inventario de archivos creados/modificados, actualizado al cierre.
- Archivos de esta continuación: AVANCE_FINAL.md, DemoCatalogSeeder.cs, DemoController.cs, Views/Demo/Index.cshtml, ScikitContentModel.cs, MlController.cs, recommendations.js, DemoSafetyChecks.cs, DemoSetupChecks.cs, DemoUiChecks.cs, MlChecks.cs, MapsChecks.cs, Checks/Program.cs, scripts/check-frontend.mjs, scripts/audit-release.ps1 y documentación.

## Próxima tarea

Los pendientes restantes requieren condiciones externas: claves Maps/billing habilitados voluntariamente, credenciales/permisos PieSocket revisados, Docker disponible y entorno Render para despliegue autorizado. Después: Maps real, revisión visual desktop/móvil, entrega real y ciclo de mensajería. No iniciar la aplicación sobre app.db ni aplicar su migración sin autorización.

## Cierre: retirada de Google Maps
Build Debug y publish Release correctos; suite completa, HTTP y frontend sin Maps aprobados. Endpoints de encuentros y /js/maps.js devuelven 404. Publicación nueva sin scripts Maps ni cartografía: 113.4 MiB. No quedan referencias GoogleMaps ni URLs Maps en el proyecto MVC. app.db sin cambios. Sin commit, push ni cambios en main. Pendientes actuales: PieSocket, entrega real, Docker/Render y revisión visual; Maps ya no es un requisito del proyecto.
