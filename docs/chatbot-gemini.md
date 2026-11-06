# Chatbot Gemini: configuración y verificación

El mensaje «El asistente no está configurado» procede de `AiSearchService.GetCompletionAsync`, antes de hacer HTTP. Indica que ninguna clave reconocida contiene un valor. No es un error de cuota ni del endpoint.

## Render

En el servicio web, abre Environment y agrega:

- `Gemini__ApiKey`: clave privada de Gemini obtenida en Google AI Studio. Obligatoria; no incluir en Git ni en JavaScript.
- `Gemini__Model`: `gemini-2.5-flash` (valor predeterminado). Debe estar disponible para tu proyecto de Google.
- `Gemini__FallbackModel`: opcional; un modelo disponible que se probará si el principal devuelve 503. Sin esta variable no se cambia de modelo.

Alternativas reconocidas: `GEMINI_API_KEY` o `GOOGLE_API_KEY` para la clave; `GEMINI_MODEL` y `GEMINI_FALLBACK_MODEL` para modelos. Usa una sola convención para evitar valores contradictorios. Se elige el primer valor no vacío, comenzando por `Gemini:*`.

Guarda y vuelve a desplegar/reinicia el servicio. User Secrets local no se publica en Render. No hace falta cambiar ASPNETCORE_ENVIRONMENT a Development.

## Desarrollo local

Desde la raíz del repositorio, sustituye el marcador únicamente en tu terminal privada:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "<TU_CLAVE_PRIVADA>" --project SegundaOportunidad
dotnet user-secrets set "Gemini:Model" "gemini-2.5-flash" --project SegundaOportunidad
```

Reinicia la aplicación en Development. No compartas la salida de `user-secrets list`.

## Verificación real

Abre el chatbot y pregunta «Explica brevemente qué es SegundaOportunidad». También puedes enviar:

```javascript
fetch('/api/chat', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ mensajeUsuario: 'Explica brevemente qué es SegundaOportunidad' })
}).then(r => r.json()).then(console.log)
```

Usa una sesión permitida por las reglas existentes de verificación de usuarios. HTTP 200 por sí solo no confirma éxito: el contrato actual también devuelve mensajes de error dentro de `respuesta`. Confirma que contiene una respuesta útil y que no hay error Gemini en los logs.

Los logs registran modelo, estado HTTP y código de Google, sin cuerpo completo ni clave. 400: solicitud/clave o restricciones; 401/403: permisos/autenticación; 404: modelo no disponible; 429: cuota; 503: indisponibilidad. Comprueba en AI Studio la clave, sus restricciones, cuota y modelos habilitados según el código recibido.

Endpoint REST: `https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent`, clave en `x-goog-api-key`.

## Pruebas sin servicios externos

```powershell
dotnet build SegundaOportunidad.slnx
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --gemini-checks
```

Cubren falta de clave, configuración vacía con alias válido, cabecera/endpoint/cuerpo JSON, respuesta de texto, HTTP 400/403/404/429/503, JSON inválido y ausencia de candidatos. No prueban la validez de una clave real ni el despliegue de Render.

Referencia: https://ai.google.dev/api/generate-content y https://ai.google.dev/gemini-api/docs/models/gemini-2.5-flash

## Diagnóstico del 404 observado

La prueba real devolvió NOT_FOUND con el motivo de que gemini-2.5-flash ya no está disponible para usuarios nuevos. Google recomendó explícitamente models/gemini-3.8-flash. ListModels aún anuncia el antiguo modelo para v1 y v1beta: ese listado no garantiza acceso a generateContent. Ambas versiones devolvieron 404 con el modelo antiguo.

Se configuró Gemini:Model=gemini-3.8-flash únicamente en User Secrets local. Para el mismo caso en Render, configura Gemini__Model=gemini-3.8-flash. No se ha modificado el valor predeterminado del código para todas las cuentas.

La ruta admite ahora un ID simple o el nombre models/ID y elimina el prefijo antes de construir la URL. Se mantiene v1beta. Los errores registran una proyección JSON segura: code numérico, status permitido y motivo clasificado (MODEL_UNAVAILABLE_TO_NEW_USERS o mensaje omitido). No se registra message/details originales, claves ni consultas.

## Verificación real completada

Se consultó ListModels en v1beta con la clave local y se filtró supportedGenerationMethods por generateContent. gemini-3.8-flash devolvió 503 en tres intentos; gemini-3.7-flash devolvió 503 y después agotó el tiempo de espera. gemini-3.1-flash-lite, presente en el catálogo, devolvió HTTP 200 y texto «OK».

La configuración local vigente es Gemini:Model=gemini-3.1-flash-lite, que reemplaza la selección anterior de 3.8 en User Secrets. En Render establece Gemini__Model=gemini-3.1-flash-lite y conserva la clave privada en Gemini__ApiKey. El valor predeterminado del código no se ha cambiado globalmente; la selección explícita evita depender de él.

Se verificó además el controlador real en un host MVC temporal en loopback: Google devolvió HTTP 200 y «OK_SEGUNDA»; POST /api/chat devolvió HTTP 200 y su propiedad respuesta coincidió exactamente con el texto de Google. El JavaScript existente usa esa propiedad mediante textContent. No se abrió el navegador ni se probó Render; el host temporal no usa la base de datos ni cambia los filtros de autorización de la aplicación principal.

AiSearchService reintenta únicamente HTTP 503, con un máximo de tres intentos por modelo y esperas cancelables de 1 y 2 segundos. Un fallback configurado conserva su comportamiento previo y también tiene tres intentos como máximo (hasta seis solicitudes si se configura otro modelo). Errores de autenticación, 404 y 429 no se reintentan automáticamente.

Para repetir la prueba real, que consume cuota:

```powershell
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --gemini-live
```

La prueba falla si no obtiene texto de Google o si ese texto no llega sin cambios al JSON del chatbot. Las pruebas simuladas --gemini-checks también comprueban el número máximo de reintentos y que no se reintenten los demás errores HTTP.
