# Asistente de catálogo y vendedores

## Alcance

El punto de entrada sigue siendo POST /api/chat. ChatAssistantService coordina ChatCatalogService, ChatSellerService y el AiSearchService existente. No hay modelos persistentes nuevos, migraciones, escrituras de negocio ni cambios en AdminGuard/Groq.

Flujo:

1. Interpretación local de términos, sinónimos, presupuesto, categoría existente, modalidad y estado. Ejemplos: bicicletas eléctricas, celulares económicos, precio menor a S/ 300, artículos para estudiar, comparación de bicicletas y celulares. También admite enlaces /Articulos/Details/ID para comparar publicaciones concretas. Cada consulta es independiente: no se guarda historial conversacional ni se interpreta automáticamente «el anterior».
2. Consulta EF parametrizada. Solo artículos activos, visibles y de vendedores autorizados (verificación completa o rol Admin), sin suspensión de cuenta/publicación vigente. No se recomiendan artículos vinculados a un intercambio completado, aunque IsActive siga siendo true. No se desactivan artículos en la base.
3. Si Algolia tiene AppId y WriteApiKey explícitos, se reutiliza AlgoliaService.SearchChatIdsAsync con máximo 50 IDs y espera de 3 segundos. Sus IDs solo ayudan a priorizar: EF siempre aplica permisos, filtros y disponibilidad. Si falla o está incompleto, EF sigue encontrando coincidencias locales. El chatbot no usa las credenciales de ejemplo del servicio compartido para decidir que Algolia está configurado.
4. Hasta 8 candidatos y sus señales públicas se envían a Gemini. Se proyectan solo los campos necesarios, sin fotos BLOB, entidades Identity, nombres, IDs de usuario, emails, DNI, hashes ni historiales administrativos. Los datos de contacto identificables en textos de publicaciones también se omiten mediante patrones.
5. Gemini devuelve únicamente IDs y códigos de motivos. El servidor valida los IDs y los motivos contra los candidatos. Las fichas, precios, descripciones y enlaces los construye el servidor; no se muestra prosa arbitraria ni HTML de Gemini para recomendaciones. Esto limita afirmaciones inventadas e instrucciones maliciosas dentro de una publicación.
6. Antes de devolver resultados, se vuelve a consultar la base y se recalculan las señales para no recomendar anuncios que se moderaron o cambiaron durante la llamada a IA. Si Gemini falla o su ranking no es válido, se muestran resultados reales indicando que no se utilizó su recomendación.

La interpretación local cubre los ejemplos indicados y sinónimos comunes; no es comprensión ilimitada de cualquier expresión. Si una consulta compleja no encuentra resultados, conviene usar tipo de artículo, presupuesto, categoría o enlaces específicos.

## Vendedores

El modelo tiene Rating con valor predeterminado 5, pero no hay tabla ni flujo de reseñas verificables. Por ello no se usa Rating como reputación real y no se afirma quién tiene mejores valoraciones.

ChatSellerService agrega en SQL los intercambios Completado (sin duplicar una transacción si figura el mismo participante dos veces), la verificación registrada y las publicaciones actualmente autorizadas. Se usan referencias «Vendedor 1», «Vendedor 2» dentro de la respuesta, para comparar sin compartir datos personales. Estas referencias no son identificadores permanentes: el usuario llega al anunciante abriendo la publicación.

El historial mostrado se limita a publicaciones disponibles y al número agregado de intercambios completados. No se consulta el historial privado de publicaciones moderadas, sanciones, reportes o decisiones administrativas. No hay campo que declare sanciones públicas, por lo que no se divulgan. Los indicadores no permiten garantizar confiabilidad ni ausencia de fraude; se informa expresamente.

## Contrato y pantalla

Se conserva respuesta y se añaden publicaciones, fuente y geminiUtilizado. Las fichas incluyen título, precio de venta cuando existe, modalidad, categoría, condición declarada, descripción, enlace local, motivos y evaluación pública. GeminiUtilizado indica si se aceptó un ranking para el catálogo; no se usa para certificar respuestas generales.

El widget conserva los controles actuales, usa textContent y crea enlaces locales con el ID del artículo. Nunca interpreta HTML ni URLs propuestas por IA. Su ancho y alto se limitan al viewport; el historial tiene scroll. Las reglas anteriores de verificación de usuarios se conservan: un usuario autenticado no aprobado sigue recibiendo 403. El chatbot no publica, reporta, sanciona ni inicia intercambios.

## Configuración

Gemini sigue usando Gemini:ApiKey / Gemini__ApiKey, modelo local validado Gemini:Model=gemini-3.1-flash-lite. En Render usar Gemini__Model=gemini-3.1-flash-lite y guardar la clave exclusivamente como variable privada. No se cambió ninguna credencial remota.

La integración Algolia del chatbot necesita Algolia__AppId y Algolia__WriteApiKey ya usados por el servicio existente. No sincroniza ni escribe el índice: únicamente consulta. Sin configuración explícita utiliza EF. La prueba real se hizo con EF local; la disponibilidad del índice compartido no se certificó ni se modificó.

## Validación

```powershell
dotnet build SegundaOportunidad.slnx
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --chat-catalog-checks
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll
```

Las pruebas de catálogo usan SQLite temporal y POST /api/chat mediante Kestrel real. Cubren filtros, ausencia de resultados, comparación, privacidad del JSON y contexto Gemini, Rating sin reseñas, IDs inventados, salida HTML inválida, fallback de Algolia, moderación durante la IA, límites de mensaje y bloqueo de usuarios sin verificación. La suite general también llama las pruebas nuevas.

Prueba real optativa (consume cuota; la ruta se abre exclusivamente en modo lectura):

```powershell
dotnet SegundaOportunidad.Checks/bin/Debug/net10.0/SegundaOportunidad.Checks.dll --chat-catalog-live SegundaOportunidad/app.db
```

Se comprobó Gemini HTTP 200 y POST /api/chat HTTP 200 con gemini-3.1-flash-lite para «Recomiéndame artículos para estudiar». Gemini ordenó libros, silla, laptop y mochila de los ejemplos locales; las fichas y enlaces se verificaron contra SQLite. El servidor de prueba es temporal y no modifica la autenticación de la aplicación principal. La interfaz no se verificó visualmente en un navegador ni en dispositivos físicos.
