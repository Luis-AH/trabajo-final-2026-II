# Verificación conjunta de módulos 1–3

## Resultado

Compilación y comprobaciones automatizadas aprobadas. La suite original usa autenticación simulada; RealIdentityChecks añade servidor HTTP aislado, Identity, cookies y antiforgery reales con SQLite en memoria. Cubre login, rol Admin, denegación de usuario común, moderación, auditoría, bloqueo de cookie existente, rechazo de nuevo login suspendido, reactivación y restauración. No modifica usuarios reales.

Se corrigió un fallo de validación: omitir estado/tipo de acción en formularios administrativos podía usar el valor cero del enum. Ahora ambos parámetros exigen presencia explícita. Se añadió prueba HTTP con token válido para la omisión de estado.

La base local app.db pasó integrity_check, foreign_key_check, coincidencia del modelo y ausencia de migraciones pendientes. Contiene dos usuarios, cinco artículos y cero intercambios. No tenía rol administrativo asignado; se respaldó y se inicializó Admin para la cuenta administrativa prevista, sin cambiar contraseñas ni crear cuentas. Ahora tiene un administrador. Las pruebas de intercambio se hacen con datos temporales: la base real no contiene intercambios para contrastar.

Se inició temporalmente la aplicación real en http://127.0.0.1:5196, con su base y configuración: arrancó, RabbitMQ conectó y declaró su cola, el catálogo respondió HTTP 200 y /Admin redirigió al formulario de login. Después se detuvo la instancia de verificación.

## Límites y advertencias

- No se certificó visualmente el diseño responsive ni la ejecución de Chart.js en un navegador.
- RabbitMQ: conexión comprobada; no se enviaron mensajes reales ni se certificó entrega completa.
- Algolia, Redis y PieSocket: operaciones reales de extremo a extremo pendientes. La prueba aislada no las utiliza.
- Persisten NU1901 de NuGet.Packaging y NuGet.Protocol 6.12.1.
- EF emite advertencia 10622 por relaciones obligatorias hacia Articulo con filtro global. Las consultas administrativas e históricas relevantes omiten el filtro explícitamente; los flujos públicos lo aplican. Las pruebas cubren preservación de intercambios y lectura administrativa tras ocultación. Al añadir consultas con Include de artículos, decidir explícitamente si deben incluir moderados.
- En el arranque temporal solo HTTP apareció advertencia de puerto HTTPS no definido; no se modificó la configuración de despliegue.

## Repetir

```powershell
dotnet build SegundaOportunidad.slnx
dotnet run --project SegundaOportunidad.Checks --no-build
dotnet run --project SegundaOportunidad.Checks --no-build -- --verify-db SegundaOportunidad/app.db
```

El último comando abre SQLite en modo solo lectura. RealIdentityChecks debe ejecutarse desde la raíz del repositorio. AdminBootstrap es una utilidad de mantenimiento para la cuenta fija prevista, no una ruta pública.

Archivos de esta revisión: BindRequired en AdminPublicacionesController y AdminUsuariosController; nuevos RealIdentityChecks, DatabaseVerification y AdminBootstrap en Checks; integración de estos controles en Program del arnés; este informe. No se avanzó al Módulo 4 ni se realizaron commits, push o PR.
