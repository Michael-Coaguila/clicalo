---
status: Aceptado
date: 2026-09-25
decision-makers: Michael Coaguila (mantenedor)
consulted: propuestas de dominio, fiabilidad y equipo; crítica del plano 1.0 (hueco «Persistencia / copias automáticas»)
informed: colaboradores y agentes, mediante AGENTS.md
---

# ADR-0007 · Documento JSON versionado con escritura atómica y el uso en un archivo aparte

## Contexto y planteamiento del problema

«Nunca se pierden datos» es una regla que no se puede romper (REG-08). Macro Quick Access la rompía de
varias formas (lecciones L-DAT-1 a L-DAT-4 del catálogo):

- el esquema no tenía versión y la identidad del perfil era su nombre visible;
- un JSON dañado se sustituía por los perfiles de fábrica y se guardaba al momento;
- los datos vivían junto al `.exe` y dentro de OneDrive, con fallos de guardado por bloqueos (WinError 5
  y 32) que solo quedaban en el registro;
- se escribía en cada arrastre, cada paso de opacidad o cada tecla de un campo.

El catálogo pide un documento de datos con escritura atómica y agrupada (DAT-001, DAT-002), no
sobrescribir nunca un documento ilegible (DAT-003), identificadores opacos (DAT-004), deshacer (DAT-006),
copia automática 30 s después de cada cambio (COP-003) y poder volver a la versión anterior de la app
(ACT-005). La crítica del plano 1.0 detectó además que registrar el uso de cada atajo dentro del
documento reescribía el archivo en cada toque y acababa llenando las 12 copias automáticas solo con
cambios de uso. ¿Qué formato y qué protocolo de escritura cumplen todo esto?

## Factores de decisión

- Durabilidad: ninguna combinación de fallo de proceso, bloqueo de archivo o disco lleno pierde el
  documento (REG-08, NFR-006).
- Volver a la versión N−1 de la app sin perder datos (ACT-005).
- Legible, comparable entre versiones y fácil de copiar como copia de seguridad.
- Pocas escrituras: 10 cambios en 1 s producen como máximo una escritura, y 1000 ejecuciones ninguna
  escritura del documento.
- Migraciones que sigan compilando cuando cambien los tipos del dominio.
- Tamaño: menos de 2 MB con 50 perfiles y 2000 atajos (NFR-016).

## Opciones consideradas

- Documento JSON con envoltorio, esquema `major.minor`, campos desconocidos conservados, escritura
  atómica con `ReplaceFileW` y cuarentena, más `usage.json` aparte
- SQLite o LiteDB
- Un diario de escritura adelantada (WAL) propio
- JSON con una versión de esquema entera, sin compatibilidad hacia delante
- El uso dentro del documento

## Resultado de la decisión

Opción elegida: **«Documento JSON versionado con escritura atómica, cuarentena y el uso aparte»**,
porque es pequeño, legible y atómico, permite volver a N−1 sin perder datos y evita que cada toque
reescriba el documento.

- **Envoltorio:** `format`, `schema {major, minor}`, `writtenBy`, `seq`, `writtenAtUtc`, `payloadSha256`
  y `payload`. El hash detecta daños; no aporta seguridad.
- **Minor = cambio aditivo.** Una versión anterior con el mismo major lee el documento y conserva los
  campos desconocidos (`[JsonExtensionData]` en la raíz, el perfil, el atajo y los ajustes).
  **Major = migración.** Una versión que encuentra un major mayor que el suyo nunca escribe: entra en
  solo lectura y ofrece la copia `pre-update`.
- **Protocolo de escritura:** serializar y calcular el hash → validar releyendo los bytes (un documento
  inválido no se guarda) → escribir `*.tmp` con `FILE_FLAG_WRITE_THROUGH` y `FlushFileBuffers` →
  `ReplaceFileW(destino, tmp, destino.prev)`. Los errores transitorios se reintentan a 50, 100, 200, 400
  y 800 ms; si persisten, aparece un estado «no guardado» visible, se reintenta cada 30 s y se hace una
  copia de emergencia.
- **Carga y recuperación:** un documento ilegible se **mueve** a `quarantine\` (nunca se borra); después
  se prueba `.prev`, luego la copia válida más reciente y, si no hay nada, un documento por defecto en
  memoria que no se escribe hasta que el usuario lo acepte.
- **DTOs separados del dominio** en Infrastructure; el mapeador valida, repara lo reparable y construye
  con `Library.CreateValidated`.
- **Migraciones** como funciones puras e idempotentes sobre `JsonObject`, en una cadena contigua, con
  copia `pre-migrate` antes. El importador de la v1 es una etapa aparte, idempotente y con límites contra
  bombas zip (`SafeZipReader`).
- **Uso aparte:** `usage.json` lleva el mismo envoltorio y un `usageEpoch` que debe coincidir con el del
  documento (lo sube «Reiniciar Frecuentes»). Se guarda con 30 s de *debounce* y 5 min de latencia
  máxima, nunca reescribe el documento ni programa copias.
- **Copias automáticas** solo por cambios en porciones significativas: 30 s después del último cambio,
  las últimas 12.
- **Ubicación:** `%AppData%\Clicalo\` (sobrevive a desinstalar), separada de la instalación
  (`%LocalAppData%\Clicalo.App\`, ADR-0012).

### Consecuencias

- Buena, porque una copia de seguridad es copiar un archivo y un documento se puede comparar entre
  versiones.
- Buena, porque volver a N−1 con el mismo major no pierde nada.
- Buena, porque el uso intensivo no toca el documento ni desplaza las copias de cambios reales.
- Mala, porque el documento se carga y se proyecta entero; es correcto a esta escala, pero no escalaría a
  órdenes de magnitud más.
- Mala, porque hay dos archivos que deben ser coherentes; `usageEpoch` resuelve el caso de un «Reiniciar
  Frecuentes» interrumpido entre las dos escrituras.
- Mala, porque cada cambio de esquema exige decidir si es minor o major y escribir la migración con sus
  *fixtures*.

### Confirmación

- `CrashingFileSystem`: ningún documento perdido en ningún punto de fallo (criterio de M2).
- *Fixtures* inmutables por versión en `tests/fixtures/schema/<major.minor>/`; cada migración nueva se
  prueba desde todas las versiones anteriores; pruebas de cadena sin huecos ni ciclos y de
  `Apply(Apply(x)) == Apply(x)`.
- Prueba de uso intensivo con `FakeTimeProvider`: 10 000 `RecordUsage` en 8 h no cambian `backups\auto\`,
  no escriben `clicalo.json` y escriben `usage.json` como máximo una vez cada 30 s.
- Spike S11 (persistencia hostil): Defender, indexador, un monitor que bloquea el archivo y sincronización
  simulada, sin ningún documento perdido y con error visible si el bloqueo dura más de 3 s.
- Importación de los 3 `profiles.json` reales: 210 → 210 atajos.

## Pros y contras de las opciones

### JSON versionado con escritura atómica y uso aparte

- Buena, porque es legible, pequeño, atómico y compatible hacia delante dentro del mismo major.
- Mala, porque exige disciplina de esquema y un protocolo de escritura propio (con sus pruebas).

### SQLite o LiteDB

- Buena, porque dan transacciones y consultas.
- Mala, porque el documento se carga entero para las proyecciones de todos modos, y una copia de
  seguridad deja de ser «copiar un archivo legible».
- Mala, porque añaden una dependencia nativa o de terceros a la cadena de suministro.

### Diario WAL propio

- Buena, porque reduciría el tamaño de cada escritura.
- Mala, porque es más código crítico que `ReplaceFileW` para un documento de menos de 2 MB.

### Versión de esquema entera

- Buena, porque es más simple.
- Mala, porque sin compatibilidad hacia delante volver a N−1 perdería los campos nuevos.

### El uso dentro del documento

- Buena, porque es un solo archivo.
- Mala, porque cada toque reescribe el documento y las 12 copias automáticas acaban conteniendo solo
  cambios de uso.

## Criterios de reapertura

Se reconsidera si la escala de datos exigida (NFR-016: 50 perfiles y 2000 atajos) crece unas 10 veces.

## Más información

- Plano: [§1.2 (D10 y D11)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§6.4](../architecture/blueprint.md#64-estado-cuatro-dueños-deshacer-y-autoguardado),
  [§6.5](../architecture/blueprint.md#65-persistencia),
  [§6.6](../architecture/blueprint.md#66-migraciones-incluida-v1--v2),
  [§6.8](../architecture/blueprint.md#68-copias).
- Catálogo: REG-08 en [§1](../requirements/catalog.md#1-reglas-que-no-se-pueden-romper-requisitos-transversales);
  DAT-001 a DAT-006 en [§2.29](../requirements/catalog.md#229-dat--datos-e-integridad); COP-003 en
  [§2.26](../requirements/catalog.md#226-cop--copias-de-seguridad); esquema v1 en
  [§7](../requirements/catalog.md#7-esquema-v1-exacto-macro-quick-access-y-conversión); lecciones
  L-DAT-1 a L-DAT-4 en [§8](../requirements/catalog.md#8-lecciones-de-la-app-antigua-y-del-prototipo).
- Paquete de diseño: [02-modelo-de-datos.md](../design/handoff/docs/02-modelo-de-datos.md).
- Registro: [critique.json](../architecture/decision-record/critique.json), hueco «Persistencia /
  copias automáticas».
- ADR relacionados: [ADR-0003](0003-cuatro-duenos-de-estado.md),
  [ADR-0008](0008-secretos-dpapi-y-administrador-de-credenciales.md),
  [ADR-0012](0012-velopack-canales-y-datos.md).
