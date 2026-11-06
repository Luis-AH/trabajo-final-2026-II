# Auditoría de Render — SegundaOportunidad

Fecha: 6 de noviembre de 2026. Rama local: feature/reciclaje-ronaldo. HEAD local: 9f9ad23.

**Estado: correcciones locales comprobadas; no desplegado ni certificado en Render.** Los cambios siguen sin commit; Render no puede construir estos archivos hasta que estén en un commit remoto autorizado. No se ejecutaron push, pull, merge, reset ni commits, ni se modificaron datos de producción.

## Diagnóstico y evidencia

| Componente | Evidencia obtenida | Pendiente real |
|---|---|---|
| Gemini | API y POST /api/chat HTTP 200, gemini-3.1-flash-lite; recomendaciones basadas en SQLite local abierto en solo lectura | Repetir con variables, catálogo y permisos de Render |
| AdminGuard | El código usa **Groq**, no Grok/xAI. Prueba real multimodal satisfactoria con qwen/qwen3.8-27b; SHA256 y auditoría en SQLite temporal | Repetir como Admin en Render, con artículo y foto de ensayo |
| SMTP | Mailtrap Sandbox aceptó por TLS/auth el correo de prueba; token temporal válido y de un solo uso | Sandbox no entrega correo a clientes reales. Configurar SMTP transaccional real y comprobar recepción desde Render |
| Algolia | Consulta real autenticada satisfactoria, 0 coincidencias para prueba. Sin escrituras al índice compartido. Fallback EF y permisos probados | Indexar una publicación desde Render y comprobar la búsqueda; consistencia de un índice compartido por entornos |
| RabbitMQ | Conexión y autenticación reales satisfactorias, sin declarar colas, publicar ni consumir mensajes compartidos | Envío, confirmación, consumo, persistencia y reconexión desde Render con dos cuentas de ensayo |
| PieSocket | Health HTTP 200; consulta autenticada members HTTP 403, cuerpo: Invalid credentials | Corregir key/secret del mismo proyecto y cluster; comprobar avisos reales y dominios permitidos |
| Redis | No hay configuración local; SQLite funciona sin caché | Si se activa: PING y operaciones desde Render; no se certificó este proveedor |
| Production | Dos arranques reales con base temporal persistida; /health, login, catálogo, CSS y API respondieron; AdminGuard exige sesión | No equivale a una prueba Linux/Docker ni al proxy real de Render |
| Data Protection | Persistencia de claves y descifrado entre proveedores/reinicios; proxy confiable aceptado y desconocido rechazado | Montaje persistente real, permisos y respaldo de claves en Render |
| Compilación | Solución compila; persisten NU1901 en NuGet.Packaging y NuGet.Protocol 6.12.1 | Resolver advertencias de dependencias en un cambio validado aparte |

### Errores corregidos

- SQLite relativo y claves efímeras en Production: ahora se requieren ruta SQLite absoluta y DataProtection:KeyPath. La plantilla usa /var/data con disco persistente. No se mueve ni sustituye ninguna base existente automáticamente.
- Docker evaluaba PORT durante el build: ahora lo evalúa al iniciar y escucha en 0.0.0.0. La carpeta de trabajo de la imagen continúa siendo /app.
- El build podía incluir bases, respaldos y configuración de desarrollo: se reforzó .dockerignore y CopyToPublishDirectory.
- Credenciales versionadas o incrustadas: los valores locales existentes se preservaron en User Secrets antes de retirarlos de appsettings y de los fallbacks de Algolia/vista PieSocket. El historial Git sigue conteniendo versiones antiguas; coordinar su rotación con los propietarios. No se rotaron credenciales remotas.
- Seeding con contraseñas conocidas: las cuentas de demostración se conservan solo en Development. En Production, una base nueva necesita BootstrapAdmin; no se elevan cuentas existentes por correo ni se cambian sus contraseñas. Una base con administradores existentes no crea otra cuenta. Las cuentas antiguas con claves de demostración deben corregirse manualmente antes de exponer el servicio.
- Fallos de inicialización de la base ya no se ocultan arrancando una web rota: se detiene el inicio con diagnóstico seguro.
- Se añade procesamiento de cabeceras reenviadas solo desde proxies/redes explícitamente confiables. No se vacían las listas de confianza ni se habilita confianza global. Cookies de autenticación Secure en Production.
- RabbitMQ conectaba en StartAsync y dejaba de consumir si fallaba: conexión en segundo plano y nuevos intentos cada 10 segundos; QoS, idempotencia y comprobación de participantes conservadas. Publicación con confirmación del broker, espera máxima de 10 segundos y respuesta 503 controlada. Un fallo al persistir se reencola; mensajes JSON inválidos se rechazan. Rechazos repetidos por almacenamiento requieren intervención y monitorización, no garantizan entrega ilimitada.
- PieSocket enviaba channelId donde la API v3 requiere channel. La vista ahora toma cluster/key desde IConfiguration. Solo se publica un aviso refresh, nunca contenido de conversaciones: el cliente recupera mensajes por una petición autenticada y mantiene el refresco periódico.
- Los IDs DOM del asistente y de la mensajería eran compartidos (chat-input/chat-messages); el widget ahora usa assistant-* para no interferir con las conversaciones. Un envío fallido ya no se presenta como exitoso ni borra el texto.
- La búsqueda pública conserva EF como respaldo cuando Algolia no está configurado, falla o no tiene registros; sin registrar consultas ni IDs de usuarios en logs.
- Gemini usa por defecto el modelo comprobado gemini-3.1-flash-lite. Las variables explícitas siguen teniendo prioridad; se mantienen reintentos limitados y errores redactados.

No hay migraciones nuevas. El arranque sigue aplicando las migraciones existentes; respaldar SQLite antes de un futuro despliegue. Las fotografías de artículos están en FotosArticulos dentro de SQLite: persistir la base conserva esas imágenes. No existe un almacenamiento independiente de fotos de perfil que esta auditoría pueda certificar.

## Tabla completa de Environment

Dos guiones bajos `__` representan `:` en IConfiguration; `__0`, `__1`, etc. representan entradas de una lista. Los marcadores entre `< >` se sustituyen por valores de TU servicio; no son valores secretos ni dominios reales. No copiar comillas alrededor del valor en el Dashboard.

| SERVICIO | KEY EXACTA EN RENDER | VALUE QUE DEBO COLOCAR | OBLIGATORIA U OPCIONAL | DÓNDE OBTENER EL VALOR |
|---|---|---|---|---|
| ASP.NET Core | ASPNETCORE_ENVIRONMENT | Production | Obligatoria para este despliegue | Valor fijo |
| Contenedor | PORT | 8080 | Opcional; fallback 8080; plantilla lo fija | Plantilla/puerto del servicio |
| SQLite | ConnectionStrings__DefaultConnection | Data Source=/var/data/app.db;Cache=Shared | Obligatoria en Production | Ruta del disco montado; conservar o migrar datos antes |
| Data Protection | DataProtection__KeyPath | /var/data/keys | Obligatoria en Production | Carpeta privada del mismo disco |
| Host | AllowedHosts | <tu-servicio>.onrender.com;<dominio-propio-si-existe> | Recomendable; default * | Dashboard/Domains; separar hosts por ; |
| Proxy HTTPS | ReverseProxy__KnownProxies__0 | <IP real y verificada del proxy inmediato> | Condicional: proxies no loopback | Logs HttpOverrides/soporte o infraestructura de Render; no inventar IP |
| Proxy HTTPS | ReverseProxy__KnownNetworks__0 | <CIDR confirmado de una red de proxies confiables> | Alternativa a IP; opcional si no hace falta | Infraestructura/soporte; nunca 0.0.0.0/0 ni ::/0 |
| Primer Admin | BootstrapAdmin__Email | <correo nuevo del primer administrador> | Obligatoria solo si la base no tiene ningún Admin | Responsable del proyecto |
| Primer Admin | BootstrapAdmin__Password | <contraseña privada fuerte y única> | Obligatoria junto con Email en base nueva | Gestor de contraseñas; no reutilizar clave de ejemplo |
| Gemini | Gemini__ApiKey | <clave privada real de Gemini> | Obligatoria para IA | Google AI Studio/API Keys |
| Gemini | Gemini__Model | gemini-3.1-flash-lite | Opcional: es el default validado; recomendable explícita | ListModels y generateContent comprobados con tu cuenta |
| Gemini | Gemini__FallbackModel | <otro modelo disponible validado> o no crear variable | Opcional; default mismo modelo, sin cambio | API oficial de Gemini; no inventar nombres |
| AdminGuard / Groq | AdminGuard__ApiKey | <clave privada de Groq, NO de xAI> | Obligatoria para AdminGuard | Groq Console/API Keys |
| AdminGuard / Groq | AdminGuard__Model | qwen/qwen3.8-27b | Obligatoria; sin default | Modelo multimodal probado con esta cuenta |
| AdminGuard / imágenes | AdminGuard__ImageHosts__0 | images.unsplash.com u otro hostname autorizado | Opcional; default images.unsplash.com | CDN de imágenes externas; fotos de SQLite no lo necesitan |
| SMTP | RecoveryEmail__Host | <hostname SMTP transaccional> | Obligatoria para correo | Panel SMTP del proveedor; no endpoint HTTP ni token sin datos SMTP |
| SMTP | RecoveryEmail__Port | 587 o puerto STARTTLS ofrecido, p. ej. 2525 | Opcional: default 587; conectividad obligatoria | Proveedor; el cliente usa STARTTLS, no TLS implícito 465 |
| SMTP | RecoveryEmail__Username | <usuario SMTP> | Obligatoria para correo | Credenciales SMTP del proveedor |
| SMTP | RecoveryEmail__Password | <contraseña SMTP o de aplicación> | Obligatoria para correo | Proveedor; no siempre coincide con un API token |
| SMTP | RecoveryEmail__From | <remitente autorizado/verificado> | Obligatoria para correo | Remitentes/dominio verificado del proveedor |
| SMTP | RecoveryEmail__PublicBaseUrl | https://<tu-servicio>.onrender.com | Obligatoria para recuperación | URL HTTPS pública real de Render, sin / final |
| Algolia | Algolia__AppId | <Application ID> | Obligatoria para Algolia; opcional para EF | Dashboard Algolia |
| Algolia | Algolia__WriteApiKey | <clave backend con permisos sobre articulos> | Obligatoria para sincronizar/buscar vía servicio actual | Algolia API Keys; nunca enviar al navegador |
| RabbitMQ | RabbitMQ__ConnectionString | <URL AMQPS completa del proveedor> | Obligatoria para enviar/persistir chat | Connection URL del broker; usuario, clave, vhost y puerto TLS correctos |
| PieSocket | PieSocket__ClusterId | <cluster real, sin https:// ni .piesocket.com> | Obligatoria para avisos en tiempo real | Panel del mismo proyecto PieSocket |
| PieSocket | PieSocket__ApiKey | <API Key de suscripción del mismo proyecto> | Obligatoria para PieSocket; visible al cliente por diseño | Panel PieSocket; restringir dominios permitidos |
| PieSocket | PieSocket__ApiSecret | <API Secret backend que corresponda a esa Key> | Obligatoria para publicar avisos | Panel PieSocket; nunca incluir en JS |
| Redis | Redis__ConnectionString | <host:puerto,password=...,ssl=True,abortConnect=False según proveedor> | Opcional; vacío usa SQLite | Proveedor Redis; formato StackExchange.Redis, no pegar redis:// sin convertir |
| Logs | Logging__LogLevel__Default | Information | Opcional; default Information | Valor fijo |
| Logs ASP.NET | Logging__LogLevel__Microsoft.AspNetCore | Warning | Opcional; default Warning | Valor fijo |
| Diagnóstico de proxy | Logging__LogLevel__Microsoft.AspNetCore.HttpOverrides | Debug durante diagnóstico, luego Warning | Opcional y temporal | Configuración de diagnóstico; sin loguear cuerpos ni credenciales |
| Host .NET | DOTNET_ENVIRONMENT | No crear; si ya existe, Production | Opcional; evitar contradicción con ASPNETCORE_ENVIRONMENT | Environment del servicio |

Alias de Gemini que el código también admite. **No son variables adicionales obligatorias**: usa una sola convención y evita valores contradictorios.

| SERVICIO | KEY EXACTA EN RENDER | VALUE QUE DEBO COLOCAR | OBLIGATORIA U OPCIONAL | DÓNDE OBTENER EL VALOR |
|---|---|---|---|---|
| Gemini, alias de clave | GEMINI_API_KEY | <misma clase de clave privada Gemini> | Alternativa a Gemini__ApiKey | AI Studio |
| Gemini, alias secundario de clave | GOOGLE_API_KEY | <clave válida para Gemini> | Alternativa secundaria; no configurar si usas la principal | Google/AI Studio |
| Gemini, alias de modelo | GEMINI_MODEL | gemini-3.1-flash-lite | Alternativa a Gemini__Model | Modelo validado |
| Gemini, alias de fallback | GEMINI_FALLBACK_MODEL | <modelo alternativo validado> | Alternativa a Gemini__FallbackModel | API de modelos |

Prioridad de los valores no vacíos: Gemini:ApiKey, GEMINI_API_KEY, GOOGLE_API_KEY; para modelos, Gemini:Model y después GEMINI_MODEL. No existen en este código AdminGuard__Endpoint, RecoveryEmail__EnableSsl ni una variable de xAI. ASPNETCORE_URLS no es necesaria con el Dockerfile: --urls se construye con PORT y tiene prioridad.

## Despliegue manual, cuando exista un commit autorizado

1. **Conservar datos primero.** Antes de cambiar una instancia existente, obtener un respaldo SQLite consistente y resguardar sus claves de Data Protection. Si se cambia de app.db relativo a /var/data/app.db, trasladar/restaurar el respaldo mediante un procedimiento controlado; la aplicación no lo hace sola. No sobrescribir la base de Render con la base de ejemplos de la PC.
2. El commit que debe desplegarse es uno futuro que incluya estas correcciones en feature/reciclaje-ronaldo. HEAD 9f9ad23 no las contiene. Revisar el diff y, únicamente cuando tú lo autorices, realizar el commit y publicación por el flujo del equipo. Esta auditoría no los ejecutó.
3. En el Web Service correcto de Render, Settings → Build & Deploy: rama feature/reciclaje-ronaldo, runtime Docker, Root Directory vacío (raíz del repositorio), Dockerfile ./Dockerfile y contexto raíz. No usar dotnet run ni un Build Command de Node. Docker realiza restore/publish. Dejar Docker Command sin override para respetar ENTRYPOINT. Desactivar Auto-Deploy si se desea solo despliegue manual.
4. Para conservar SQLite, elegir un plan de pago compatible y añadir disco /var/data, una sola instancia. La plantilla render.yaml propone 0.5c-512mb y 1 GB; es configuración local y **no creó ni contrató recursos**. Revisar capacidad/costo en Render antes de aplicarla. Si el servicio ya existe, modificar sus Settings/Environment/Disks; no crear otro accidentalmente usando Blueprint.
5. En Environment, añadir las variables de la tabla para las funcionalidades utilizadas. No trasladar User Secrets como archivo ni pegar valores en appsettings. SMTP real requiere un remitente autorizado. El health y los logs ayudan a confirmar el proxy; configurar las IP/redes confiables obtenidas de la infraestructura, nunca habilitar confianza para cualquier dirección.
6. Guardar variables mediante Save (o la opción de guardar sin desplegar si aparece). Si eliges Save, rebuild, and deploy, ya inicia un despliegue. Para control manual, guardar primero y después Manual Deploy → Deploy a specific commit, eligiendo el SHA autorizado; alternativamente Deploy latest commit solo después de comprobar qué SHA corresponde a la rama.
7. Revisar Build Logs: restore y publish correctos. En Deploy Logs: servidor escuchando, migraciones finalizadas, sin errores de BootstrapAdmin ni almacenamiento. El puerto se evalúa al iniciar. /health debe responder 200 y el Dashboard indicar Live. Live solo confirma que la web arrancó y superó su health check; no certifica las APIs externas.
8. Ingresar con un administrador, cambiar cualquier contraseña histórica de demostración y retirar BootstrapAdmin__Password del entorno tras el primer arranque satisfactorio. No eliminar las claves persistidas de Data Protection: invalidaría cookies, tokens y enlaces existentes.
9. Ejecutar la matriz de aceptación de abajo con cuentas y publicaciones de ensayo identificadas, sin modificar datos reales ajenos. Volver a comprobar fotos, sesión y persistencia tras un reinicio manual del servicio.

### Aceptación desde Render

| Función | Acción concreta | Evidencia necesaria |
|---|---|---|
| Salud/HTTPS | Abrir https://TU_HOST/health y login | HTTP 200, sin bucles de redirección; cookies Secure y proxy configurado |
| Gemini/catálogo | Preguntar por productos de ensayo, presupuesto y categoría; probar una búsqueda sin resultados | POST /api/chat 200, geminiUtilizado=true cuando hay ranking válido, enlaces a artículos reales; no basta un mensaje de error dentro de HTTP 200 |
| Permisos del chatbot | Probar con cuenta aprobada y no aprobada; buscar artículo oculto | 403 para usuario autenticado no aprobado; nunca aparece el artículo oculto |
| AdminGuard | Como Admin, analizar foto propia de ensayo; consultar estadísticas y un reporte de ensayo | Resultado persistido con fecha/modelo/hash cuando corresponde; acceso denegado a no Admin |
| SMTP | Recuperar cuenta de ensayo con email confirmado y recibir el correo | Mensaje recibido, URL HTTPS del dominio correcto, token válido una vez; Sandbox no prueba entrega a usuarios |
| Algolia | Publicar un artículo de ensayo desde la web; buscar un término único | Registro sincronizado y búsqueda real. Ocultarlo y comprobar que no se recomienda aunque el índice esté atrasado |
| Mensajería | Dos usuarios aprobados se envían un texto de ensayo | SendMessage 200 solo si broker confirma, persistencia en SQLite, recepción; probar reinicio y caída temporal del broker |
| PieSocket | Credenciales válidas y dominio de Render permitido; mantener dos sesiones | Aviso new_message recibido, actualización autenticada, nunca texto privado en el payload del socket |
| Redis | Si se configuró, probar acceso al dashboard y luego caída de Redis | Caché funciona o fallback sin error; no es requisito de persistencia |
| SQLite/fotos/keys | Subir foto de ensayo y reiniciar/re-desplegar | Datos y foto sobreviven; sesión/token persisten según su vigencia, disco montado y backups comprobados |

### Logs e interpretación

- Gemini: HTTP 400 indica solicitud/configuración, 401/403 autorización, 404 modelo no disponible, 429 cuota, 503 saturación. Se registran estado y cuerpo proyectado/redactado; no claves ni consultas completas. Tres intentos por modelo ante 503 con esperas de 1/2 s; fallback adicional solo si se configuró.
- Groq: 401/403 clave/permisos, 404 modelo/ruta, 429 límite, 400 parámetros o imagen incompatible. No usar una clave de xAI contra api.groq.com.
- SMTP: error/timeout puede ser puerto bloqueado o configuración del proveedor. SmtpClient usa STARTTLS con límite de 15 s. El mensaje genérico de recuperación no confirma envío por diseño, para evitar enumeración de cuentas.
- Algolia: comprobar AppId, API Key y permisos del índice articulos. Fallback EF evita que la búsqueda dependa exclusivamente del índice, pero no demuestra que Algolia esté operativo.
- RabbitMQ: «consumidor activo» confirma conexión; errores seguros muestran tipo y reintento. No se registran URI, credenciales ni contenido de mensajes. Vigilar reencolados repetidos y disco lleno.
- PieSocket: el diagnóstico local exacto fue 403 / Invalid credentials. Usar Key y Secret correspondientes al MISMO proyecto/cluster, no copiar solo la clave de suscripción antigua. No registrar cuerpos de errores ni secretos.
- Almacenamiento: startup detenido por ruta relativa, falta de KeyPath, error de migraciones o falta de BootstrapAdmin en base nueva significa que el despliegue no está listo. No cambiar Production a Development para eludirlo.
- Reverse proxy: Unknown proxy requiere confirmar el origen antes de añadirlo. No usar ASPNETCORE_FORWARDEDHEADERS_ENABLED=true como atajo que acepte cualquier proxy.

## Límites y verificaciones pendientes

Render Free no admite disco persistente y bloquea salida SMTP en 25/465/587; 25 también se bloquea en planes de pago. El disco solo preserva lo escrito dentro del montaje. Manteniendo SQLite, el camino propuesto es un servicio de pago con una sola instancia. Un proveedor con puerto STARTTLS 2525 puede ser alternativa si está disponible y su conectividad se verifica; esta auditoría no la garantiza. Free además se suspende por inactividad: no es adecuado para prometer consumo continuo de RabbitMQ.

La publicación Release también pasó y se comprobó que no incluye app.db ni appsettings.Development.json. Docker no está disponible en este entorno: no se construyó ni ejecutó la imagen Linux. La suite completa, incluyendo DeploymentChecks, pasó tras los cambios finales. La revisión comparando valores locales de credenciales contra archivos publicables no encontró coincidencias. Sí se compiló la solución y se arrancó la aplicación real en Production dos veces con datos temporales. Pendientes: corregir PieSocket, entrega SMTP transaccional real, ciclo RabbitMQ/PieSocket completo, proxy y almacenamiento montado reales, backups/restauración, límites y costos del plan, revisión de dependencias NU1901. Las claves de Data Protection se guardan en archivo; proteger acceso al disco y respaldos (no están cifradas por un certificado de aplicación configurado).

## Archivos corregidos y pruebas reproducibles

- Infraestructura: Dockerfile, render.yaml, .dockerignore, SegundaOportunidad.csproj, Program.cs y nuevo DeploymentConfiguration.cs.
- Inicio seguro: DataSeeder.cs; configuración versionada: appsettings.json.
- Integraciones: AiSearchService.cs (default validado), AlgoliaService.cs, ArticulosController.cs, RabbitMqPublisher.cs, ChatBackgroundService.cs, PieSocketService.cs, MensajesController.cs y Views/Mensajes/Index.cshtml, además del aislamiento de IDs del widget en Views/Shared/_Layout.cshtml.
- Pruebas: DeploymentChecks.cs, ProductionSmokeChecks.cs, IntegrationReadChecks.cs y registro en Checks/Program.cs. Los hosts aislados de pruebas de catálogo/Gemini usan puertos dinámicos para no colisionar al correr en paralelo.

```powershell
dotnet build SegundaOportunidad.slnx
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --deployment-checks
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --production-smoke
```

Las pruebas reales optativas consumen cuota o envían exclusivamente a Mailtrap Sandbox; las comprobaciones de infraestructura no escriben en los servicios compartidos:

```powershell
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --integration-read
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --chat-catalog-live SegundaOportunidad/app.db
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --guard-live
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --smtp-live
```

## Fuentes oficiales contrastadas

- [Render Free: almacenamiento, SMTP e inactividad](https://render.com/docs/free).
- [Discos persistentes y rutas](https://render.com/docs/disks).
- [Docker y variables](https://render.com/docs/docker), [Environment](https://render.com/docs/configure-environment-variables), [despliegues manuales](https://render.com/docs/deploys).
- [Blueprint](https://render.com/docs/blueprint-spec), [planes y equivalencias](https://render.com/docs/compute-plans).
- [Microsoft: proxy, HTTPS y listas de confianza](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer).
- [Groq Chat Completions](https://console.groq.com/docs/api-reference), [visión](https://console.groq.com/docs/vision).
- [PieSocket: API v3/v4, channel y credenciales](https://piehost.com/docs/3.0/rest-api).
- [RabbitMQ: confirmaciones de publicación](https://www.rabbitmq.com/tutorials/tutorial-seven-dotnet).
