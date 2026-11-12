# Cómo mostrar las recomendaciones al docente

Actualización: 12/11/2026. La interfaz ya no repite etiquetas «DEMO» ni muestra el aviso superior de simulación. Las cuentas siguen siendo ficticias y las imágenes son ilustrativas; no representan ventas ni verificaciones auténticas. Los IDs internos se mantienen para no romper relaciones de artículos, intercambios e historial. Configuración de presentación en Render: `docs/RENDER_PRESENTACION.md`.

## Recorrido recomendado

1. Abrir `http://localhost:5194/Articulos`. Bajar hasta **Una selección para ti**. Las tarjetas se revelan al entrar en pantalla, escalonadas cada 130 ms, con desplazamiento y opacidad. No hay espera artificial para consultar el modelo. Si el sistema tiene movimiento reducido, aparecen sin animación.
2. Buscar **libros**, pulsar Buscar y volver a la sección de recomendaciones. Comprobar títulos, imágenes, enlaces y el indicador **Afinidad textual**.
3. Cambiar a **bicicleta deporte** y después **ropa abrigo**. Las tarjetas deben cambiar porque el servidor transforma cada consulta con el vocabulario/IDF aprendidos y ordena por coseno. La búsqueda normal del catálogo y las recomendaciones son mecanismos distintos; una frase puede no coincidir literalmente en el catálogo y aun así producir recomendaciones.
4. Abrir una publicación. En **También podrían interesarte**, el vector del artículo pasa a ser la referencia. Comparar el detalle de una computadora con el de un libro.
5. Iniciar sesión con Mateo y luego con Valeria, abrir el perfil y comparar **artículos sugeridos**. Las preferencias se construyen con publicaciones/intercambios de cada cuenta y se excluyen sus propios artículos. No se simulan clics ni popularidad que no exista.
6. Buscar **zzzzzzzzzzzzzzzzzzzz** para comprobar el estado sin coincidencias. No deben inventarse productos.

La afinidad en porcentaje es la similitud del texto escalada de 0 a 100. No mide calidad, confiabilidad, satisfacción ni probabilidad de compra. Una bicicleta puede recomendar un casco porque comparten contenido; no es un filtro exacto de categoría.

Al recargar, la web solicita una selección rotativa dentro de candidatos afines calculados por el modelo. Conserva los puntajes y las restricciones de visibilidad. Si existen suficientes alternativas, cambia parte de las publicaciones; con pocas coincidencias pueden repetirse o cambiar solo de orden. El contador se guarda por búsqueda/artículo en la sesión del navegador, sin guardar publicaciones ni datos personales. No es aprendizaje nuevo por recarga ni reentrenamiento automático. Para inspeccionar el ranking inicial determinista de la API, omitir `rotacion` o usar `rotacion=0`.

## Accesos de simulación

Se conservan las contraseñas cortas, ahora asociadas a estos correos:

| Nombre ficticio | Correo |
|---|---|
| Renata Ramos (administración) | sofia.ramos@example.invalid |
| Mateo Torres | mateo.torres@example.invalid |
| Valeria Soto | valeria.soto@example.invalid |
| Diego Flores | diego.flores@example.invalid |
| Camila Rojas | camila.rojas@example.invalid |
| Sebastián Vega | sebastian.vega@example.invalid |
| Lucía Castro | lucia.castro@example.invalid |
| Andrés Mendoza | andres.mendoza@example.invalid |
| Paula Medina | paula.medina@example.invalid |
| Nicolás Ruiz | nicolas.ruiz@example.invalid |

Contraseñas únicamente en `.demo/accounts.private.json`, ignorado por Git. No usar correos anteriores demo01…demo10 tras actualizar el catálogo. Los dominios .invalid no reciben correo real.

## Evidencia técnica

- Modelo real: Scikit-learn TF-IDF de caracteres 3–5 y similitud del coseno; ajuste offline, transformación e inferencia .NET. El chatbot Gemini es otra integración.
- Textos actualizados: 48 publicaciones, 40 elegibles; 3226 características. Se reentrenó el artefacto tras retirar prefijos y cambiar descripciones.
- Evaluación sintética de seis consultas: Precision@5=0.8667, nDCG@5=0.892337, cobertura@5=0.675. No es validación con compradores reales. Las métricas previas pertenecían a otros textos y no se reutilizan como resultado actual.
- Suite de regresión y paridad Python/.NET aprobadas. DOM simulado: animación al entrar en viewport, demoras, movimiento reducido, texto seguro, fotos locales y score real. No sustituye una revisión visual del navegador.
- API de diagnóstico: `/api/ml/recomendaciones?consulta=libros&cantidad=5`. Ese enlace muestra JSON; la presentación para usuarios está en las tarjetas de la web.

## Fotografías

Ocho imágenes ilustrativas por categoría, descargadas de Unsplash y servidas desde `wwwroot/images/catalog/`, asignadas a las 48 publicaciones. Una imagen de categoría puede repetirse y no demuestra el modelo, propiedad ni estado del objeto concreto. No son fotos subidas por los vendedores.

Procedencia, URL, licencia y SHA256: `wwwroot/images/catalog/sources.json`. [Licencia de Unsplash](https://unsplash.com/license). Sin API Key, sin hotlink al cargar las tarjetas y sin coste de API. No se confunden con las fotos reales que un usuario puede subir al publicar.

La actualización de cuentas/artículos se hizo únicamente en `.demo/segunda-demo.db`, con respaldo previo y sin modificar app.db ni credenciales de usuarios reales. El seeder usa el nuevo catálogo en bases nuevas; no reescribe automáticamente cuentas existentes.
