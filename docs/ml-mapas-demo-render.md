> ESTADO ACTUAL: Google Maps y puntos de encuentro retirados por solicitud del usuario. No configurar claves Maps ni activar facturacion. Las referencias a pruebas de mapas restantes son historicas.

# Recomendaciones, ubicaciones y demo gratuita

Actualización local: 11 de noviembre de 2026. Rama `feature/reciclaje-ronaldo`. Sin commit, push ni despliegue. Esta guía actualiza la propuesta de Render de `auditoria-render.md`: el Blueprint principal ahora es gratuito y efímero; la alternativa con disco se conserva en `render.persistent.yaml` y requiere contratar un plan compatible.

## Implementación

- ML: `ml/train.py`, corpus reproducible y exportador SQLite de solo lectura; `ScikitContentModel`, `MlRecommendationService`, `MlController` y `recommendations.js`. Inicio, catálogo, detalle y perfil muestran tarjetas con enlaces reales. No se reemplazó Gemini ni Algolia.
- Demo: `DemoCatalogSeeder`, `DemoController`, vista `/Demo`, ilustraciones propias y pruebas. Diez cuentas ficticias, 48 artículos y seis intercambios pendientes; 40 artículos elegibles, con casos ocultos/suspendidos/eliminados/inactivos para comprobar exclusión. Sin reseñas ficticias. Las aprobaciones técnicas DEMO no se presentan como identidad auténtica.
- Ubicaciones: servicios de Google, polígonos abiertos del Perú, selección de punto en crear/editar, ubicación pública aproximada y propuesta/confirmación privada de encuentros. La edición comprueba propietario y concurrencia; no permite cambiar moderación ni dueño mediante campos manipulados.
- Interfaz: inicio y catálogo renovados, filtros y paginación de 12 tarjetas, estilos compartidos para perfil, intercambios, administración, autenticación y chatbot. Iconos locales MIT, foco visible y reducción de animaciones. Se conservan formularios, CSRF y cierre de sesión.

## ML reproducible y sus límites

Python 3.12 y dependencias fijadas en `ml/requirements.txt`. El entrenamiento usa **Scikit-learn 1.6.1**, TF-IDF de caracteres de 3 a 5 y coseno. Título tiene peso 3 y categoría 2; descripción, modalidad y estado, peso 1. Normalización NFKD a alfanuméricos ASCII y espacios. No hay ajuste dentro de una petición HTTP.

Desde la raíz, con un Python propio y un entorno virtual:

```powershell
python -m venv .venv
.venv/Scripts/python -m pip install -r ml/requirements.txt
.venv/Scripts/python ml/create_demo_corpus.py
.venv/Scripts/python ml/train.py --input ml/demo-corpus.json
```

`ml/artifacts/tfidf.joblib` conserva vectorizador/matriz, y `evaluation.json` el informe. Ambos quedan ignorados. El JSON exportado `SegundaOportunidad/App_Data/recommender.json` contiene vocabulario, IDF, versión, corpus y casos de paridad; se publica con la app. C# reproduce transformación y coseno: no necesita Python, FastAPI ni otro servicio en Render. El hash identifica el artefacto completo. No se deben cargar joblib de fuentes no confiables.

Para entrenar sobre artículos locales autorizados, sin modificar la base:

```powershell
.venv/Scripts/python ml/export_catalog.py --db .demo/segunda-demo.db
.venv/Scripts/python ml/train.py --input ml/artifacts/catalog.json
```

Ese comando sustituye deliberadamente el artefacto público; revisar qué textos se publicarán antes de usar un catálogo diferente. No exporta Identity, correos, DNI ni sanciones. El modelo publicado actualmente contiene exclusivamente texto sintético DEMO. Las publicaciones nuevas se transforman con el vocabulario existente; vocabulario desconocido puede dar similitud nula. Reentrenar explícitamente cuando cambie el catálogo.

| Evaluación sintética, seis consultas | Inicial | Final |
|---|---:|---:|
| Documentos elegibles | 40 | 40 |
| Características | 3155 | 3168 |
| Precision@5 media | 0.8667 | 0.9000 |
| nDCG@5 media | 0.8899 | 0.9117 |
| Cobertura@5 | 0.6500 | 0.6750 |

Informes completos: `ml/evaluation-demo-baseline.json` y `ml/evaluation-demo.json`. Relevancia definida por categoría sintética, sin conjunto de usuarios reales ni evaluación de satisfacción; no demuestra generalización. La consulta de laptop conserva Precision@5 de 0.4 bajo esas etiquetas: no se oculta ese caso. La mejora corresponde a ponderar campos, no a una prueba independiente.

API: `GET /api/ml/recomendaciones?consulta=libros&cantidad=5`, o `articuloId=ID_REAL`. Cantidad entre 1 y 12. Solo considera hasta 2000 publicaciones recientes autorizadas, activas, visibles, no completadas y de vendedores habilitados. Revalida permisos antes de responder. Nunca devuelve publicaciones propias como recomendaciones al usuario autenticado. Con historial usa sus publicaciones e intercambios; sin historial usa el centroide del catálogo, sin afirmar popularidad. Es afinidad textual, no garantía de calidad, seguridad o precio conveniente. Un modelo ausente devuelve 503 controlado.

## Base y cuentas DEMO

La migración aditiva `20261111183102_UbicacionesPeruEncuentros` añade columnas opcionales a Articulo e Intercambio y versión de concurrencia para encuentros. No elimina tablas ni registros. Se probó en SQLite temporal y `.demo/segunda-demo.db`. **No se aplicó a `SegundaOportunidad/app.db`**. Evitar arrancar con la conexión predeterminada hasta autorizar esa aplicación: el inicializador existente ejecuta migraciones al iniciar.

Preparación local explícita, después de compilar:

```powershell
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --demo-setup .demo/segunda-demo.db
```

Se comprueba idempotencia, contraseñas con Identity, diez usuarios y 48 artículos. Las credenciales aleatorias están solo en `.demo/accounts.private.json`, ignorado por Git y Docker. No hay contraseña compartida fija. No publicar ese archivo. El seed no se ejecuta automáticamente y solo permite Development/Demo. En la web, `/Demo` requiere Admin y POST con CSRF; las nuevas contraseñas se muestran una sola vez al crearlas. No reescribe cuentas existentes ni reseñas.

Arranque local aislado (no utiliza app.db ni consume el broker compartido):

```powershell
$demoDb = (Resolve-Path .demo/segunda-demo.db).Path
dotnet run --project SegundaOportunidad --no-launch-profile -- --environment Development --Demo:UseDataset true --ConnectionStrings:DefaultConnection "Data Source=$demoDb;Cache=Shared" --RabbitMQ:ConnectionString= --urls http://localhost:5194
```

La opción `Demo:UseDataset` evita las cuentas antiguas de desarrollo cuando se usa este conjunto explícito. No usar Development en Render. La configuración vacía del broker en ese comando es solo aislamiento de la demo local.

## Google Maps retirado

Por solicitud del usuario (11/11/2026), se eliminaron selector, buscador, mapas, enlaces y acuerdos de punto de encuentro, servicios Google, controlador/endpoints, JavaScript y cartografia. No se necesitan claves ni facturacion de Maps. Publicaciones, intercambios, fotos, chatbot y recomendaciones se conservan.

No se borraron datos ni columnas: la migracion aditiva y el esquema se conservan por compatibilidad con las bases de ensayo donde ya se habian aplicado. No se aplico ninguna migracion a app.db. Los resultados anteriores sobre Maps son historicos y no describen la funcionalidad vigente.

## Render gratuito y variables

`render.yaml` usa plan free, entorno Demo, sin disco ni servicio Python adicional. No se desplegó ni contrató nada. `render.persistent.yaml` conserva la alternativa anterior con disco, que no es gratuita. El Dockerfile publica el artefacto y datos geográficos; no incorpora Python portable ni credenciales DEMO.

| Variable de Render | Valor/configuración | Necesidad |
|---|---|---|
| ASPNETCORE_ENVIRONMENT | `Demo` | Demo pública, nunca Development |
| PORT | `8080` | Contenedor |
| ConnectionStrings__DefaultConnection | `Data Source=/tmp/segunda-demo/app.db;Cache=Shared` | SQLite efímero |
| DataProtection__KeyPath | `/tmp/segunda-demo/keys` | Cookies/tokens, también efímeros |
| BootstrapAdmin__Email | Cuenta propia de administración | Base nueva; secreto/config privada |
| BootstrapAdmin__Password | Contraseña privada robusta | Base nueva; no versionar |
| AllowedHosts | Host exacto de la app | Recomendado |
| ReverseProxy__KnownProxies__0 | IP del proxy verificada | Solo si corresponde; no inventar |
| ReverseProxy__KnownNetworks__0 | CIDR verificado del proxy | Alternativa por red; no confiar en todas |
| Gemini__ApiKey | Clave Gemini | Chatbot real; secreto |
| Gemini__Model | Modelo habilitado en esa cuenta | Última prueba: gemini-3.1-flash-lite |
| Gemini__FallbackModel | Otro modelo previamente comprobado, o dejar sin definir | Opcional; no inventar nombres |
| AdminGuard__ApiKey | Clave **Groq** | AdminGuard; no es xAI/Grok |
| AdminGuard__Model | Modelo multimodal habilitado | Nueva prueba satisfactoria: qwen/qwen3.8-27b |
| AdminGuard__ImageHosts__0 | Dominio HTTPS autorizado de fotos externas | Opcional; fotos locales usan lector existente |
| Algolia__AppId | ID de aplicación | Búsqueda remota; EF es fallback |
| Algolia__WriteApiKey | Clave con permisos del índice actual | Secreto; nunca al navegador |
| RabbitMQ__ConnectionString | URI amqps del proveedor | Mensajería; secreto |
| PieSocket__ClusterId | Cluster del proyecto | Avisos en tiempo real |
| PieSocket__ApiKey | Key del mismo proyecto | Suscripción/publicación |
| PieSocket__ApiSecret | Secret correspondiente | Solo servidor |
| Redis__ConnectionString | URI/config válida | Opcional, fallback sin caché |
| RecoveryEmail__Host | Servidor SMTP | Recuperación/confirmación |
| RecoveryEmail__Port | Puerto STARTTLS permitido por proveedor/alojamiento | No asumir conectividad |
| RecoveryEmail__Username | Usuario SMTP | Puede ser diferente del email de cuenta |
| RecoveryEmail__Password | Contraseña/token SMTP apropiado | Secreto; API HTTP token no siempre sirve |
| RecoveryEmail__From | Remitente autorizado por proveedor | No necesariamente usuario de login |
| RecoveryEmail__PublicBaseUrl | `https://TU_HOST` | Enlaces públicos correctos |
| Ml__ModelPath | Omitir para App_Data/recommender.json | Opcional, artefacto ya incluido |
| Logging__LogLevel__Default | `Information` | No activar logs con cuerpos/secretos |

Gemini también admite alias antiguos GEMINI_API_KEY/GOOGLE_API_KEY, GEMINI_MODEL y GEMINI_FALLBACK_MODEL; preferir una sola configuración consistente. `Demo__UseDataset=true` es la opción de desarrollo local anterior, no se necesita en Render Demo. No configurar ASPNETCORE_FORWARDEDHEADERS_ENABLED para eludir la lista de proxies.

En base nueva inicia BootstrapAdmin, entra a `/Demo` y pulsa preparar explícitamente. Guarda de forma privada las credenciales mostradas. La demo es idempotente mientras exista la base. Un reinicio/redeploy/suspensión con reemplazo de filesystem puede perder usuarios, fotos, mensajes, claves, cookies y tokens; después se repite bootstrap y seed. No promete persistencia, backups ni operación continua del consumidor RabbitMQ. No cargar datos reales confidenciales en esta demo. Revisar [Render Free](https://render.com/docs/free) y [Blueprint](https://render.com/docs/blueprint-spec).

Free bloquea puertos SMTP 25/465/587; un puerto alternativo STARTTLS como 2525 depende del proveedor y debe probarse desde Render. La prueba local de Mailtrap Sandbox no demuestra entrega transaccional ni conectividad desde Render. El artefacto preentrenado evita el consumo de RAM de Python durante HTTP; no se midió la memoria en Linux/Render ni se ejecutó Docker aquí.

### Tabla de servicios solicitada

| Servicio | Variable exacta en Render | Valor esperado | Obligatoria u opcional | Dónde obtenerla |
|---|---|---|---|---|
| ASP.NET | ASPNETCORE_ENVIRONMENT | Demo | Obligatoria para esta demo | Blueprint incluido |
| Contenedor | PORT | 8080 | Obligatoria | Blueprint incluido |
| SQLite | ConnectionStrings__DefaultConnection | Data Source=/tmp/segunda-demo/app.db;Cache=Shared | Obligatoria | Blueprint; sin proveedor externo |
| Identity | DataProtection__KeyPath | /tmp/segunda-demo/keys | Obligatoria | Blueprint incluido |
| Admin inicial | BootstrapAdmin__Email / BootstrapAdmin__Password | Email propio y contraseña privada fuerte | Obligatorias al crear la base | Elegidas por administrador, Dashboard Environment |
| Host | AllowedHosts | Dominio Render exacto | Recomendable | URL asignada al servicio |
| Proxy | ReverseProxy__KnownProxies__0 / ReverseProxy__KnownNetworks__0 | IP/CIDR comprobado | Según infraestructura | Información verificada del proxy/alojamiento |
| Gemini | Gemini__ApiKey | Secreto Gemini | Obligatoria para Gemini real | Google AI Studio, API Keys |
| Gemini | Gemini__Model | ID comprobado mediante models/generateContent | Obligatoria para fijar modelo | API oficial listModels; cuenta propia |
| Gemini | Gemini__FallbackModel | Segundo ID comprobado | Opcional | Misma API y cuenta |
| AdminGuard/Groq | AdminGuard__ApiKey / AdminGuard__Model | Clave Groq y modelo multimodal vigente | Obligatorias para AdminGuard real | Groq Console, API Keys y modelos |
| Imágenes AdminGuard | AdminGuard__ImageHosts__0 | Host HTTPS permitido | Opcional | Dominio autorizado de imágenes usadas |
| Algolia | Algolia__AppId / Algolia__WriteApiKey | Application ID y clave del servidor | Opcionales para búsqueda EF; necesarias para Algolia | Algolia Dashboard, API Keys del índice existente |
| RabbitMQ | RabbitMQ__ConnectionString | URI amqps completa | Obligatoria para mensajes reales | Panel del broker existente |
| PieSocket | PieSocket__ClusterId / PieSocket__ApiKey / PieSocket__ApiSecret | Tres valores del mismo proyecto | Necesarias para avisos en tiempo real | Panel PieSocket, mismo cluster/proyecto |
| Redis | Redis__ConnectionString | Configuración del servicio | Opcional | Proveedor Redis, si se utiliza |
| SMTP | RecoveryEmail__Host / RecoveryEmail__Port | Servidor y puerto STARTTLS accesible | Necesarias para email real | Credenciales SMTP del proveedor |
| SMTP | RecoveryEmail__Username / RecoveryEmail__Password | Usuario y secreto SMTP | Necesarias para email real | Credenciales SMTP, no asumir API token HTTP |
| SMTP | RecoveryEmail__From | Remitente autorizado | Necesaria para email real | Dominio/remitente verificado por proveedor |
| SMTP | RecoveryEmail__PublicBaseUrl | https://TU_HOST | Necesaria para email real | URL HTTPS de la aplicación |
| Scikit-learn | Ml__ModelPath | Ruta al JSON exportado | Opcional; incluido por defecto | Entrenamiento local; no requiere API Key |
| Logs | Logging__LogLevel__Default | Information | Opcional | Configuración del administrador |

No hay credenciales nuevas de Scikit-learn ni un segundo servicio de pago. Los proveedores pueden cambiar disponibilidad/cuotas: consultar sus paneles, no reutilizar claves de otros servicios.

### Verificación después de publicar

1. Configurar variables privadas en Render y comprobar que `render.yaml` corresponde a Demo free. Cuando se autorice un commit/despliegue, construir el Dockerfile de esta rama; no usar main ni `render.persistent.yaml` por accidente.
2. Abrir `/health`, login y `/Articulos`. Verificar HTTPS/cookie Secure y proxy confiable, sin bucles. Si el proxy no es reconocido, comprobar su IP antes de añadirla.
3. Con BootstrapAdmin abrir `/Demo` y preparar datos explícitamente. Guardar sus credenciales una sola vez. Esperar diez usuarios y 48 publicaciones, 40 elegibles.
4. Abrir `/api/ml/recomendaciones?consulta=libros&cantidad=5`; comprobar IDs, enlaces y que artículos ocultos/suspendidos no aparezcan. Iniciar dos usuarios DEMO para contrastar preferencias sin revelar datos privados.
5. Con claves Maps habilitadas probar crear/editar en Perú, intento exterior, caída de API y formulario manipulado; confirmar que no guarda coordenadas inválidas. Proponer encuentro entre participantes, confirmar con contraparte y comprobar denegación a un tercero.
6. Probar Gemini con una búsqueda real, AdminGuard tras resolver cuota, recuperación a correo autorizado y mensajería entre dos cuentas. Exigir evidencia de respuesta/entrega, no solo HTTP 200 genérico. Mailtrap Sandbox no entrega a usuarios reales.
7. Revisar desktop y móvil con teclado y consola JavaScript. Comprobar filtros, tarjetas, chat, logout y formularios. Esta revisión visual aún no se realizó aquí.
8. Comprobar y aceptar expresamente que un reemplazo/redeploy puede borrar toda la demo; volver a entrar con BootstrapAdmin y repetir seed. No presentar esta opción como persistente.

Archivos de despliegue: Dockerfile, proyecto MVC, vistas/wwwroot, migraciones, App_Data/recommender.json, polígonos y metadatos/licencias, y corpus DEMO enlazado por csproj. `ml/` permite reproducir entrenamiento desde el repositorio; no es dependencia Python del contenedor. Nunca incluir `.demo`, `.tools`, `.venv`, joblib privado, app.db, `.env` o User Secrets.

## Evidencias de cierre

Comandos ejecutados el 11/11/2026:

```powershell
dotnet build SegundaOportunidad.slnx
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --demo-ui-checks
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --production-smoke
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --demo-smoke
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --chat-catalog-live .demo/segunda-demo.db
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --integration-read
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --guard-live
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --smtp-live
```

| Prueba | Resultado |
|---|---|
| Build completo | 0 errores; NU1901 existentes de NuGet.Packaging/Protocol 6.12.1 |
| Suite de regresión | Aprobada, incluyendo Identity, moderación, reportes, fotos, chatbot y migraciones temporales |
| ML real | Paridad Python/.NET, error máximo 1.3878E-15; scoring de API comprobado, exclusión de contenido oculto, preferencias y cold start |
| Ubicaciones/encuentros | Polígonos reales + proveedor simulado; rechazo de países/puntos inválidos, CSRF, privacidad y confirmación por contraparte |
| Vistas reales HTTP | Público, login/register, admin y usuario 200; edición propia, rechazo ajeno y creación con coordenadas manipuladas comprobados sobre copia temporal |
| Arranque Production | Dos arranques reales sobre base temporal, tokens/keys persistidos entre ambos; sin servicios externos |
| Arranque Demo | Dos arranques sobre base nueva temporal, ML disponible, seed sin ejecución automática y preparación exige sesión |
| Gemini real | generateContent 200, modelo gemini-3.1-flash-lite; POST /api/chat 200, resultados verificados contra SQLite DEMO de solo lectura |
| Groq real | Primera prueba 429; nueva prueba multimodal satisfactoria con qwen/qwen3.8-27b, SHA256 y auditoría temporal persistidos |
| Algolia | Consulta real correcta, cero coincidencias; sin indexar |
| RabbitMQ | Conexión/autenticación correcta; sin enviar ni consumir |
| PieSocket | Health 200, members 403; credenciales/permisos pendientes, sin publicación |
| Redis | Opcional, no configurado |
| SMTP | Mailtrap Sandbox aceptó TLS/auth; token temporal válido una sola vez |

Revisión visual desktop/móvil **pendiente**: Computer Use se detuvo porque no pudo verificar la URL de la ventana de Edge con suficiente confianza. No se intentó eludir ese bloqueo. Las pruebas HTTP comprueban renderizado y rutas, no apariencia en pantalla, clics JavaScript ni funcionamiento real de Maps. Diseño responsive implementado, sin certificar visualmente.

No se aplicó migración a app.db (1204224 bytes; última escritura 06/11/2026 13:15:51). No se modificaron datos de producción ni servicios remotos. Pendientes reales: claves/billing de Maps y prueba completa, revisión visual, autorización PieSocket, entrega SMTP real, ciclo de mensajería completo, build Docker y despliegue Render. La demo no soluciona persistencia gratuita de SQLite.

La publicación Release pasó: App_Data incluye modelo, corpus y cartografía; no contiene app.db, appsettings.Development.json, cuentas privadas, joblib ni .env. Comparación de 17 valores privados locales (sin mostrarlos) contra 303 archivos versionables: sin coincidencias. Esto no sustituye una auditoría de todo el historial Git. `git diff --check` pasó. Persisten advertencias NU1901; no se cambiaron paquetes de los compañeros para ocultarlas.

## Auditoría autónoma adicional

El seed ahora exige base separada sin usuarios normales ni actividad ajena a DEMO; admite conservar el administrador inicial. La operación completa es transaccional y una mezcla detectada en la web devuelve 409 con explicación. El CLI solo prepara rutas dentro de `.demo` o temporales y rechaza app.db antes de migrar. No aplicar esta herramienta a otra base de trabajo.

Se añadieron comprobaciones de rollback, Production prohibido, idempotencia, CSRF y errores HTTP; modelo corrupto/ausente, no-store y siete casos de artículos no autorizados; propuestas/confirmaciones obsoletas 409 y nueva confirmación al cambiar punto. La suite completa pasó con 303 líneas OK de comprobaciones; no se presentan como 303 pruebas independientes de un framework.

Reentrenar en otra carpeta reprodujo exactamente el artefacto SHA256 `64CB0D74D8CFAE97B5D179271B84A6BEF92CF4551DDCD57C5A9C3083803689CA`. Sin cambios en métricas. La prueba Groq posterior completó imagen y auditoría; el 429 anterior no se repitió.

```powershell
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --demo-safety-checks
node scripts/check-frontend.mjs
powershell -NoProfile -File scripts/audit-release.ps1
```

Node >=18 se utiliza solo en comprobaciones locales, no en Render. La prueba usa DOM y API Maps simulados, sin navegador ni descargas de Google: cubre texto malicioso sin innerHTML, enlaces locales, imágenes seguras, límite 12, vacío, 503, HTML inesperado, desconexión y timeout de 15 s; el estado aria-busy se restaura. Maps: regiones PE, Places centra sin suministrar el punto, clic explícito/limpieza y error de clave. No demuestra apariencia ni conectividad Google.

La auditoría Release comprueba 11 recursos y exclusiones conocidas, hash del modelo, Blueprint free sin disco; resultado correcto, 118 MiB de archivos. Ese tamaño no equivale a RAM ni garantiza arranque Linux. No está instalado Docker en este equipo. PieSocket conserva la ruta y JSON V3 respaldados por [su documentación oficial](https://piehost.com/docs/3.0/rest-api); el 403 exige revisar autorización/credenciales en su panel, no inventar otra ruta ni cambiar claves automáticamente.

Seguimiento para retomar: `AVANCE_FINAL.md`. Inventario: `docs/archivos-ml-mapas-demo.md`. Registro de regresión local: `.demo/checks-autonomos.log`, ignorado por Git.

Cierre de esta continuación: build completo 0 errores; `--demo-smoke` pasó dos arranques nuevos, API y protección del seed. Comparación final de secretos configurados/contraseñas DEMO contra 308 archivos versionables, sin coincidencias. No se cambió app.db, no se inició la web habitual y no se ejecutaron operaciones de escritura Git.
