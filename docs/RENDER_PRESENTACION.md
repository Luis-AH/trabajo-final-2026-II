# Presentación en Render: usuarios, catálogo y recomendaciones

Guía vigente: 12/11/2026. Rama `feature/reciclaje-ronaldo`. Google Maps retirado. Esta guía sustituye las instrucciones antiguas de preparación manual y BootstrapAdmin para el Blueprint free.

## Qué contiene GitHub

Código MVC, modelo preentrenado de Scikit-learn, corpus de 48 publicaciones, diez nombres/correos ficticios, ocho fotografías locales con procedencia/licencia y scripts de entrenamiento/pruebas. Docker publica todos estos recursos; no requiere Python en ejecución ni API para ML o imágenes. Renata sigue entrando con `sofia.ramos@example.invalid`.

Los usuarios se crean mediante Identity con hashes y roles al arrancar una base de presentación nueva. Las contraseñas se reciben como secreto de Render: **no se publica la SQLite local ni las contraseñas en GitHub**. El catálogo incluye 48 registros de prueba; 40 son visibles y disponibles. Los ocho restantes cubren estados ocultos, suspendidos, eliminados o inactivos, sin saltarse moderación.

## Configuración mínima exacta

| Variable Render | Valor |
|---|---|
| ASPNETCORE_ENVIRONMENT | `Demo` (nombre interno del entorno; no muestra un aviso en la web) |
| PORT | `8080` |
| ConnectionStrings__DefaultConnection | `Data Source=/tmp/segunda-demo/app.db;Cache=Shared` |
| DataProtection__KeyPath | `/tmp/segunda-demo/keys` |
| SampleData__InitializeOnStartup | `true` |
| SampleData__CredentialsJson | Contenido privado de `.demo/render-accounts.private.json` |

No hace falta BootstrapAdmin con este modo: la primera cuenta del conjunto es administradora. No usar Development en Render. No habilitar el modo de presentación sobre una base de producción; el inicializador está prohibido en Production. Si ya existen los diez perfiles y 48 artículos, conserva los cambios posteriores y no vuelve a sembrarlos. Si una base vacía necesita inicializarse pero faltan credenciales o hay datos ajenos, falla con explicación sin mezclar registros.

## Archivo privado listo para importar

Se genera localmente, con los valores existentes y sin imprimirlos:

```powershell
powershell -NoProfile -File scripts/export-render-env.ps1
```

Si ya conoces tu dominio:

```powershell
powershell -NoProfile -File scripts/export-render-env.ps1 -PublicBaseUrl https://TU_SERVICIO.onrender.com
```

Resultado: `.demo/render-presentation.private.env`. Contiene las variables mínimas, las diez contraseñas locales y las integraciones disponibles en User Secrets. Está ignorado por Git y Docker. No se exporta la URL localhost del correo. No sustituye automáticamente variables proxy/host que ya tengas configuradas en Render.

En Render: seleccionar el servicio → **Environment → Add from .env** → importar el archivo → revisar valores → **Save, rebuild, and deploy**. Confirmar que el servicio construye la rama `feature/reciclaje-ronaldo`, usando Dockerfile y contexto raíz del repositorio. El Blueprint `render.yaml` configura free y despliegue manual; no elegir `render.persistent.yaml`, que es una alternativa de pago.

Referencia oficial: [variables y carga desde .env](https://render.com/docs/configure-environment-variables), [Blueprint](https://render.com/docs/blueprint-spec).

## APIs y servicios opcionales

| Función | Variables exactas | De dónde salen |
|---|---|---|
| Recomendaciones Scikit-learn | Ninguna clave; modelo incluido | Entrenamiento local `ml/train.py` |
| Fotografías de catálogo | Ninguna clave; imágenes incluidas | `wwwroot/images/catalog/sources.json` |
| Chatbot Gemini | `Gemini__ApiKey`, `Gemini__Model`, opcional `Gemini__FallbackModel` | Google AI Studio y modelos habilitados de la cuenta |
| AdminGuard | `AdminGuard__ApiKey`, `AdminGuard__Model` | Groq Console; no es xAI/Grok |
| Algolia | `Algolia__AppId`, `Algolia__WriteApiKey` | Algolia Dashboard; hay fallback EF |
| Mensajería | `RabbitMQ__ConnectionString` | Proveedor RabbitMQ existente |
| Avisos instantáneos | `PieSocket__ClusterId`, `PieSocket__ApiKey`, `PieSocket__ApiSecret` | Mismo proyecto/cluster PieSocket; último diagnóstico 403 |
| Correo | `RecoveryEmail__Host`, `RecoveryEmail__Port`, `RecoveryEmail__Username`, `RecoveryEmail__Password`, `RecoveryEmail__From`, `RecoveryEmail__PublicBaseUrl` | Proveedor SMTP y dominio HTTPS de Render |
| Redis | `Redis__ConnectionString` | Opcional; no se usa para conservar el contador de publicaciones |

No inventar valores de claves. La exportación reutiliza las credenciales locales sin cambiarlas; no demuestra disponibilidad/cuotas desde Render. Mailtrap Sandbox no entrega correo a usuarios reales. Render free bloquea SMTP 25/465/587; puerto alternativo como 2525 depende del proveedor y se debe probar. Configurar proxies de confianza mediante `ReverseProxy__KnownProxies__0` o `ReverseProxy__KnownNetworks__0` únicamente con IP/CIDR verificados; no confiar en todos los proxies para forzar HTTPS.

## Contadores y aceptación

En el inicio aparecen **40 artículos disponibles en el catálogo**, separados de **publicados por ti**. Renata tiene cero propios porque administra; Mateo y Valeria tienen seis cada uno. El total disponible excluye artículos moderados, inactivos, de cuentas no habilitadas o intercambiados/completados. Las cifras se consultan en la base cada vez y no se quedan una hora en caché.

Pruebas antes de presentar:

1. Abrir `/health`, `/Articulos`, login y perfil. Deben verse fotos, nombres nuevos y recomendaciones sin el aviso superior ni la etiqueta técnica solicitada.
2. Entrar con Renata: total catálogo 40 y propios 0. Entrar con Mateo: propios 6.
3. Crear una publicación válida: total disponible pasa a 41 y el contador propio aumenta inmediatamente. Ocultarla como Admin: desaparece del catálogo y de las recomendaciones.
4. Buscar libros, bicicletas y ropa; observar recomendaciones escalonadas y afinidad textual. La afinidad no es garantía de seguridad/calidad.
5. Si reinicia una base existente, no debe duplicar artículos ni borrar nuevas cuentas. Si Render reemplaza el filesystem, recrea el conjunto inicial desde GitHub y el secreto de credenciales.

Se probaron localmente dos arranques reales en entorno Demo, carga desde JSON secreto, login HTTP con cookies Secure, 10 cuentas/48 registros, fotos empaquetadas, contador 40→41 sin caché y conservación de nueva actividad tras reiniciar. La suite de regresión y comprobaciones de publicación pasaron. Esto **no es un despliegue ni prueba real de Docker/Linux/Render**: falta configurar el servicio en el panel y verificar su URL/proxy. No se creó ningún servicio de pago.

## Limitación de la opción gratuita

Render free pierde cambios del filesystem al reiniciar, redeplegar o suspender la instancia. Este paquete recupera usuarios y publicaciones iniciales; **no conserva nuevas publicaciones, mensajes, contraseñas cambiadas, sesiones ni fotos cargadas después** si se pierde la SQLite efímera. No prometer persistencia de producción. Datos iniciales y fotos incluidas en la imagen sí se reconstruyen. Fuente: [Render Free](https://render.com/docs/free).

El archivo `.demo/segunda-presentacion-release.zip`, cuando se genera, contiene la publicación Release sin datos privados. El despliegue Docker de Render usa GitHub; no necesita subir ese ZIP ni el archivo de base de datos.
