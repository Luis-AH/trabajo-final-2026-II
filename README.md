# SegundaOportunidad

Plataforma universitaria para publicar, comprar, donar e intercambiar artículos con una comunidad. Incluye recomendaciones de contenido entrenadas mediante Scikit-learn y un asistente Gemini que consulta publicaciones reales autorizadas.

## Tecnologías y funciones

| Componente | Implementación |
|---|---|
| Aplicación | ASP.NET Core MVC, .NET 10, Razor y Bootstrap |
| Datos y usuarios | EF Core, SQLite e Identity con roles |
| Recomendaciones | Scikit-learn TF-IDF + coseno; entrenamiento Python offline, inferencia .NET |
| Asistentes | Gemini para usuarios; AdminGuard con Groq para revisión administrativa |
| Búsqueda | Algolia con alternativa EF Core |
| Mensajería | RabbitMQ y avisos PieSocket |
| Correo/caché | SMTP y Redis opcional |
| Despliegue | Docker y Blueprint de presentación para Render Free |

Catálogo con fotos, categorías, filtros, precios y paginación; perfiles, publicaciones propias, intercambios, conversaciones, reportes, moderación, suspensiones e historial. Las cuentas normales necesitan aprobación administrativa para participar. Una aprobación o sugerencia de IA no garantiza identidad auténtica ni ausencia de fraude.

Google Maps fue retirado: no se requieren claves ni facturación de Maps.

## Ejecutar localmente

Requiere el SDK de .NET 10. Python no es necesario para arrancar con el modelo ya incluido. Desde la raíz:

```powershell
dotnet restore SegundaOportunidad.slnx
dotnet build SegundaOportunidad.slnx
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --demo-setup .demo/segunda-demo.db

$catalogDb = (Resolve-Path .demo/segunda-demo.db).Path
dotnet run --project SegundaOportunidad --no-launch-profile -- --environment Development --Demo:UseDataset true --ConnectionStrings:DefaultConnection "Data Source=$catalogDb;Cache=Shared" --RabbitMQ:ConnectionString= --urls http://localhost:5194
```

Abrir **http://localhost:5194**. El comando usa una base separada y desactiva RabbitMQ para no consumir mensajes compartidos durante la prueba. Para mensajería real debe configurarse un broker adecuado y omitirse esa anulación vacía.

El conjunto contiene diez perfiles ficticios, 48 publicaciones, seis solicitudes y ocho fotografías ilustrativas por categoría. Inicialmente hay **40 artículos disponibles**; los restantes cubren estados moderados/inactivos. El contador global se distingue de los artículos propios: Renata administra y tiene cero propios; Mateo y Valeria tienen seis cada uno.

Las credenciales se generan en `.demo/accounts.private.json`, ignorado por Git y Docker. Renata Ramos administra con `sofia.ramos@example.invalid`; los demás perfiles figuran en [la guía de presentación](docs/mostrar-machine-learning.md). Los correos `.invalid` no reciben email real. Las credenciales antiguas del README anterior no corresponden a este conjunto.

No iniciar contra una base de trabajo sin revisar su conexión: el inicializador aplica migraciones al arrancar. El flujo anterior usa exclusivamente la base de presentación, sin modificar `SegundaOportunidad/app.db`.

## Machine Learning y chatbot

El modelo usa título, descripción, categoría, modalidad y estado. Se entrena fuera de las peticiones HTTP; .NET consume el vocabulario y los IDF exportados. Consulta registros actuales y excluye artículos no autorizados, ocultos, suspendidos, eliminados o no disponibles. Las recomendaciones aparecen en inicio, catálogo, perfil y detalle, con animación progresiva y opción de movimiento reducido.

```text
GET /api/ml/recomendaciones?consulta=libros&cantidad=5
GET /api/ml/recomendaciones?articuloId=ID_REAL&cantidad=4
POST /api/chat
```

La API devuelve JSON; la presentación para usuarios está en las tarjetas. El perfil considera publicaciones/intercambios propios; sin historial utiliza contenido representativo, sin inventar popularidad. Gemini es una integración distinta del recomendador.

Cada recarga de las tarjetas pide una selección rotativa entre coincidencias afines. Cambia publicaciones cuando hay alternativas suficientes, manteniendo sus puntajes y permisos. No reentrena el modelo. La API conserva el ranking inicial con `rotacion=0`; la web incrementa ese parámetro por contexto en la sesión del navegador.

Evaluación actual: 40 documentos, 3226 características y seis consultas sintéticas; Precision@5 **0.8667**, nDCG@5 **0.8923**, cobertura@5 **0.675**. No es validación con compradores reales. La afinidad textual no mide calidad, seguridad ni probabilidad de compra.

Reentrenamiento con Python 3.12:

```powershell
python -m venv .venv
.venv/Scripts/python -m pip install -r ml/requirements.txt
.venv/Scripts/python ml/create_demo_corpus.py
.venv/Scripts/python ml/train.py --input ml/demo-corpus.json
```

Artefacto publicado: `SegundaOportunidad/App_Data/recommender.json`. Vectorizador/matriz privados: `ml/artifacts/tfidf.joblib`. No cargar joblib de fuentes desconocidas. Exportación local de solo lectura: `ml/export_catalog.py`.

## Render: cuentas y catálogo al iniciar

Usar `render.yaml`, Docker y la rama `feature/reciclaje-ronaldo`. Guía vigente: [RENDER_PRESENTACION.md](docs/RENDER_PRESENTACION.md).

| Variable mínima | Valor |
|---|---|
| ASPNETCORE_ENVIRONMENT | `Demo` |
| PORT | `8080` |
| ConnectionStrings__DefaultConnection | `Data Source=/tmp/segunda-demo/app.db;Cache=Shared` |
| DataProtection__KeyPath | `/tmp/segunda-demo/keys` |
| SampleData__InitializeOnStartup | `true` |
| SampleData__CredentialsJson | Opcional para catálogo/ML. Define las contraseñas conocidas de las diez cuentas |

```powershell
powershell -NoProfile -File scripts/export-render-env.ps1 -PublicBaseUrl https://TU_SERVICIO.onrender.com
```

Importar `.demo/render-presentation.private.env` en **Render → Environment → Add from .env**, revisar valores y desplegar la rama. No subir el archivo privado ni la SQLite a GitHub. La carga automática solo permite Demo/Development con opción explícita; nunca Production. Si el conjunto ya existe, no se reemplaza ni duplica. Este modo no requiere BootstrapAdmin.

Con la carga automática habilitada, las contraseñas de esas diez cuentas se sincronizan al iniciar con `SampleData__CredentialsJson`. Ese secreto también permite reparar accesos que conservaron contraseñas aleatorias de una preparación anterior. Consultar el apartado de acceso de la guía de Render.

Si falta el JSON, el catálogo y las recomendaciones públicas arrancan con cuentas protegidas por contraseñas aleatorias. Para entrar con las claves locales se configura el JSON privado y se reinicia. El recomendador incluido funciona con las APIs externas sin configurar.

**Render Free es efímero:** puede perder SQLite, sesiones, mensajes y fotos nuevas al reiniciar/reemplazar la instancia. El conjunto inicial se reconstruye desde el paquete y sus secretos; las operaciones posteriores requieren almacenamiento persistente para conservarse. `render.persistent.yaml` es una alternativa de pago.

## Integraciones y secretos

Usar User Secrets en desarrollo y variables en Render; no escribir claves en código.

| Función | Variables |
|---|---|
| Gemini | `Gemini__ApiKey`, `Gemini__Model`, opcional `Gemini__FallbackModel` |
| AdminGuard/Groq | `AdminGuard__ApiKey`, `AdminGuard__Model` |
| Algolia | `Algolia__AppId`, `Algolia__WriteApiKey` |
| RabbitMQ | `RabbitMQ__ConnectionString` |
| PieSocket | `PieSocket__ClusterId`, `PieSocket__ApiKey`, `PieSocket__ApiSecret` |
| SMTP | `RecoveryEmail__Host`, `RecoveryEmail__Port`, `RecoveryEmail__Username`, `RecoveryEmail__Password`, `RecoveryEmail__From`, `RecoveryEmail__PublicBaseUrl` |
| Redis | `Redis__ConnectionString`, opcional |

ML e imágenes incluidas no necesitan API Key. Las fotos pueden repetirse por categoría y no prueban propiedad ni estado de un objeto. Procedencia y licencia: [sources.json](SegundaOportunidad/wwwroot/images/catalog/sources.json).

## Comprobaciones

```powershell
dotnet build SegundaOportunidad.slnx
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --ml-checks
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --demo-safety-checks
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --demo-ui-checks
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --render-catalog-checks
node scripts/check-frontend.mjs
powershell -NoProfile -File scripts/audit-release.ps1
```

Las pruebas de vistas requieren preparar antes la base de presentación. Node solo se usa en pruebas con DOM simulado, no en Render. Checks es un ejecutable: `dotnet test` por sí solo no ejecuta estas comprobaciones.

Estado local registrado: compilación, regresión, permisos, paridad Python/.NET, fotos y carga automática aprobados. Persisten NU1901. Pendientes: despliegue real Docker/Render, revisión visual escritorio/móvil y entrega completa de mensajería/correo real. PieSocket tuvo un diagnóstico 403; Mailtrap Sandbox no demuestra entrega final. Las pruebas locales no certifican el despliegue.

## Documentación

- [Render y variables exactas](docs/RENDER_PRESENTACION.md)
- [Cómo mostrar el Machine Learning](docs/mostrar-machine-learning.md)
- [Arquitectura y verificaciones históricas](docs/ml-mapas-demo-render.md)
- [Aprobación y permisos](docs/aprobacion-obligatoria.md)
- [Registro de avances](AVANCE_FINAL.md)

Repositorio grupal: [Luis-AH/trabajo-final-2026-II](https://github.com/Luis-AH/trabajo-final-2026-II). Rama de esta presentación: `feature/reciclaje-ronaldo`.
