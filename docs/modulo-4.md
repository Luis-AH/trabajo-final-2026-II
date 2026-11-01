# Módulo 4: reportes y moderación

La página se dejó apagada a petición del usuario. Para iniciar: `dotnet run --project SegundaOportunidad --launch-profile http` desde la raíz.

## Uso

Usuario autenticado: abrir el detalle de una publicación ajena y elegir Reportar publicación o Reportar comportamiento del usuario. El chat ofrece un enlace para reportar al usuario de la conversación activa. Formulario con motivos Spam, Publicidad engañosa, Posible estafa, Imagen sospechosa, Contenido inapropiado y Otro; descripción/evidencia textual de 10–2000 caracteres y enlace HTTPS opcional de hasta 1000 caracteres. La confirmación muestra referencia y estado, accesible únicamente al denunciante. No revela identidades ni justificaciones administrativas a terceros.

Admin: `/AdminReportes` o Reportes y moderación desde `/Admin`. Filtros por estado y motivo, veinte filas por página. El detalle permite leer evidencia y consultar publicación, perfil administrativo y antecedentes del denunciado, incluso cuando el artículo está moderado. Los enlaces de evidencia se abren externamente; el servidor no los descarga ni afirma haberlos verificado. No se incorporó carga de archivos en este módulo.

Transiciones permitidas: Pendiente → EnRevision o Descartado; EnRevision → Resuelto o Descartado. Las decisiones exigen confirmación, justificación de 10–2000 caracteres y token de versión. Cerrados no se reabren. Cada transición registra administrador, fecha UTC, estado previo/nuevo y justificación. El alta registra denunciante, fecha, motivo y evidencia. Estado y decisión se guardan juntos.

Las sanciones y la moderación se aplican en los formularios existentes de los módulos 2 y 3, con confirmación independiente. El detalle del reporte enlaza ambos y solicita incluir la referencia del reporte en el motivo; esa relación es textual, no una clave foránea. Resolver un reporte no aplica sanciones automáticamente. Reportes recibidos no significa infracciones confirmadas.

Solo se admite un reporte abierto por denunciante y objetivo, respaldado por índice único. Puede abrirse otro tras cerrar el anterior. No se permiten autorreportes. Para publicaciones se determina al propietario desde SQLite, no desde un campo manipulable del formulario. Los reportes de artículos no visibles no se admiten desde el flujo público.

Dashboard: conteos globales de abiertos (Pendiente + EnRevision) y resueltos. Usuarios: total de reportes recibidos y enlace a antecedentes desde el perfil. No se introdujo IA, notificaciones RabbitMQ ni sanciones automáticas en esta fase.

## Datos y verificación

Migración ReportesModeracion agrega Reportes y DecisionesReportes con relaciones restringidas, índices y versión de concurrencia. Se respaldó y actualizó la base local mediante la utilidad de migraciones; no se insertaron denuncias ficticias en app.db. Los datos de prueba permanecen en SQLite temporal.

```powershell
dotnet build SegundaOportunidad.slnx
dotnet run --project SegundaOportunidad.Checks --no-build
dotnet run --project SegundaOportunidad.Checks --no-build -- --verify-db SegundaOportunidad/app.db
```

Pruebas con Identity/cookies reales: envío HTTP, privacidad del detalle, autorización de bandeja, antiforgery, Razor y resolución. Pruebas de servicio: duplicados, autorreporte, URL insegura, transición inválida, versión obsoleta, cierre definitivo, historial, ausencia de sanciones automáticas y nuevo reporte tras cierre. Se vuelven a ejecutar las comprobaciones de módulos anteriores. Pendiente revisión visual en navegador y prueba manual con dos cuentas.

Archivos nuevos: Models/Reporte.cs, Services/ReporteService.cs, Controllers/ReportesController.cs y AdminReportesController.cs, dos vistas de Reportes y dos de AdminReportes, migración/designer y ReportChecks. Modificados: DbContext/snapshot, Program, vistas de detalle de artículos y chat, dashboard/modelo/servicio, perfil y tabla administrativos de usuarios/modelos/controlador, registro del servicio en los hosts de prueba. No se hicieron commits, push ni PR. Persisten advertencias NuGet previas.
