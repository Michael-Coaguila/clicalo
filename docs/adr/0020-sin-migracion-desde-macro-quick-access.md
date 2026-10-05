---
status: Aceptado
date: 2026-10-03
decision-makers: Michael Coaguila (dueño del producto y mantenedor)
consulted: decisión D1 del usuario del 2026-10-03; ADR-0007; plano §1.2 (D11), §6.5, §6.6 y §6.8; catálogo REG-08, §2.30 (MIG) y §7
informed: colaboradores y agentes, mediante el catálogo, el plano, LEEME-VINCULANTE.md y el CHANGELOG
---

# ADR-0020 · Sin migración desde Macro Quick Access v1

## Contexto y planteamiento del problema

El plano (decisión D11 de §1.2 y §6.6) y [ADR-0007](0007-documento-json-versionado.md) preveían una etapa de
importación desde Macro Quick Access, la app anterior del autor: leer su `profiles.json` v1 (y sus respaldos por
idioma y su `.zip`), convertirlo al documento 1.0 de Clícalo con un informe, guardar una copia byte a byte en
`backups\v1-original-*` y mostrar una pantalla previa en la bienvenida y una tarjeta de migración en Sistema ›
Copias. El catálogo lo pedía en REG-08, en los requisitos MIG-001 a MIG-009, BIE-002, COP-001 y los casos
EC-MIG-01 a EC-MIG-05, y describía el esquema v1 en su §7. M2 lo construyó: `Clicalo.Domain.Migration.V1`,
`Clicalo.Infrastructure.Migration` (con `SafeZipReader`), la opción `--migrate-v1` de `Clicalo.exe`, la orden
`anonymize-v1` de la herramienta de desarrollo, *fixtures* anonimizados de los archivos reales y textos propios.

El 2026-10-03 el usuario, dueño del producto, decidió (D1) que **Clícalo no se basa en nada de la app anterior**:
se elimina solo la capacidad de leer el `profiles.json` v1 y las piezas que existían únicamente para ella. Todo lo
demás del Prototipo v4 se mantiene, incluida la importación y la exportación del formato **propio** de Clícalo
(copias, Combinar o Reemplazar, compartir un perfil) y las migraciones entre versiones del esquema propio
(`major.minor`). Quitar la etapa cambia un formato persistido (el tipo de copia `v1-original`), un contrato
público (la línea de órdenes de `Clicalo.exe`) y un límite de confianza (el contenido importado que se acepta),
así que la regla 5 de AGENTS.md exige este ADR.

¿Qué queda de la etapa v1 y qué sustituye a sus garantías?

## Factores de decisión

- La decisión del usuario manda sobre el catálogo y el plano (§0.1 del catálogo, nivel 1).
- REG-08 (nunca se pierden datos) sigue siendo MUST: solo deja de cubrir la migración.
- Menos superficie de ataque: el contenido no confiable que se acepta se reduce al formato propio con su esquema
  (LOG-006, T10 del modelo de amenazas).
- Ningún resto: código, pruebas, *fixtures*, textos, límites y documentación que solo existían para la v1 se
  eliminan, no se dejan sin usar.
- Nada del formato propio cambia: el documento 1.0, su envoltorio, las copias y la cadena de migraciones del
  esquema siguen como los fija ADR-0007.

## Opciones consideradas

- Quitar la etapa v1 entera y declarar retirados sus requisitos
- Conservar la etapa v1 oculta, sin pantalla ni tarjeta, solo con `--migrate-v1`
- Conservar el lector v1 como un formato más de Importar (MIG-009) y quitar solo la bienvenida

## Resultado de la decisión

Opción elegida: **«Quitar la etapa v1 entera»**, porque es lo que decidió el usuario y porque una etapa oculta o
reducida seguiría exigiendo mantener un lector de contenido no confiable, sus límites contra bombas zip y sus
*fixtures* para una función que el producto ya no ofrece.

Esta decisión **sustituye la parte v1 de ADR-0007** (la frase «El importador de la v1 es una etapa aparte,
idempotente y con límites contra bombas zip (`SafeZipReader`)» y la confirmación «Importación de los 3
`profiles.json` reales: 210 → 210 atajos») y **la segunda mitad de la decisión D11 del plano** («importador v1
independiente, idempotente y con límites contra bombas zip»). El resto de ADR-0007 y la primera mitad de D11
(migraciones como funciones puras sobre `JsonObject`) siguen vigentes.

Se elimina:

- el módulo `Clicalo.Domain.Migration.V1` (lector de combinaciones, conversor, planificador, informe) y su fila de
  la matriz de módulos (`architecture/domain-modules.json`, §4.3 del plano);
- `Clicalo.Infrastructure.Migration`: `V1Reader`, `V1Importer` y `SafeZipReader` con sus límites. `SafeZipReader`
  no lo usa ninguna otra función: las copias y los perfiles compartidos son JSON, no `.zip`;
- la opción `--migrate-v1` de `Clicalo.exe`, la marca `migration-v1.pending` y el cableado del arranque
  (`StartupDocuments` solo carga el documento o, en una instalación nueva, la semilla);
- el tipo de copia `BackupKind.V1Original`, `IBackupService.KeepV1OriginalAsync` y los archivos
  `backups\v1-original-*`;
- la orden `anonymize-v1` de `tools/Clicalo.DevCli` y su lista de nombres públicos;
- las pruebas y los *fixtures* de la v1 (incluidos los anonimizados de los archivos reales);
- los textos `migT` y `migD` del paquete, declarados en la sección `retired` de
  `data/i18n/handoff-import.json` (no se importan y la prueba del texto visible idéntico no los exige), y los
  añadidos `migTProfiles`, `migTShortcuts`, `migFailT`, `migFailD`, `migRetry` y `migReportT`, con los marcadores
  `{profiles}` y `{shortcuts}`;
- los límites `Timings.Import.Zip*`, `Timings.Import.V1Max*` y `Timings.Backups.MigrationCardVisibility`.

Se conserva:

- la importación y la exportación del formato propio (COP-002, COP-005, DAT-007) y su lector
  `DocumentImportReader`, con sus límites `Timings.Import.Share*`;
- la cadena de migraciones del esquema propio y la copia `pre-migrate` (§6.6, `BackupKind.PreMigrate`), que no
  tiene que ver con la v1;
- las teclas del catálogo que la v1 usaba (` \ [ ] ' #, Win derecha) y sus grafías de texto: son parte del
  selector de teclas (EDI-008, EDI-009);
- SIS-005 (convivencia con Macro Quick Access en ejecución): no lee sus datos, evita la doble inyección.

### Consecuencias

- Buena, porque desaparece un lector de contenido no confiable con su lógica contra bombas zip; el único contenido
  importado es el formato propio, validado contra su esquema.
- Buena, porque el arranque es más simple: sin marca de migración pendiente ni copia `pre-migrate` de reintento.
- Buena, porque el repositorio deja de guardar *fixtures* derivados de archivos personales, aunque estuvieran
  anonimizados.
- Mala, porque quien venga de Macro Quick Access tiene que volver a crear sus atajos; la bienvenida lo compensa
  con el kit inicial (decisión D2 del mismo día).
- Neutral, porque ninguna versión publicada escribió `backups\v1-original-*`: no hay datos que conservar.

### Confirmación

- `ModuleMatrixTests` comprueba que la matriz de `architecture/domain-modules.json` coincide con la de §4.3, ya sin
  `Migration.V1`.
- `StartupDocumentsTests.A_profiles_json_of_the_previous_app_is_never_read_nor_touched` (REG-08): un
  `profiles.json` en la carpeta de datos no se lee ni se modifica.
- `HandoffImporterTests` prueba la sección `retired` de la receta; `StringsParityTests`,
  `VisibleTextSnapshotTests` y `HandoffTextGoldenTests` comprueban que `migT` y `migD` son las únicas claves
  retiradas y que todas las demás del paquete se conservan con su texto visible.
- Siguen en verde las pruebas del formato propio: `DocumentImportReaderTests`, `ProfileShareCodecTests`,
  `SchemaFixtureTests`, `SchemaVersionTests`, `BackupServiceTests` y `DocumentRepositoryTests`.
- Revisión: una búsqueda de `v1`, `profiles.json`, `Macro Quick Access`, `migT`, `anonymize` y `SafeZip` en `src`,
  `tests`, `tools` y `data` solo encuentra la sección `retired`, las pruebas que la comprueban, los comentarios que
  remiten a este ADR y medidas históricas de la app antigua (los 20 ms de DIS-95).

## Pros y contras de las opciones

### Quitar la etapa v1 entera

- Buena, porque cumple la decisión del usuario sin restos.
- Buena, porque reduce el contenido no confiable aceptado y el código que hay que mantener.
- Mala, porque se pierde trabajo de M2 ya probado (210 → 210 atajos).

### Conservar la etapa oculta con `--migrate-v1`

- Buena, porque no borra trabajo hecho.
- Mala, porque contradice «no se basa en nada de la app anterior» y deja un contrato de línea de órdenes y un
  lector no confiable sin función visible.

### Conservar el lector v1 solo en Importar

- Buena, porque quien quiera podría traer sus atajos a mano.
- Mala, porque mantiene MIG-002 a MIG-009, `SafeZipReader` y los textos del informe, justo lo que el usuario decidió
  quitar.

## Criterios de reapertura

Solo una nueva decisión del usuario. Si algún día se quisiera importar de otra app, se escribiría un ADR nuevo
para un importador de ese formato, sin recuperar este código.

## Más información

- Plano: [§1.2 (D11)](../architecture/blueprint.md#12-tabla-de-decisiones-clave),
  [§4.3](../architecture/blueprint.md#43-módulos-por-capacidad),
  [§6.5](../architecture/blueprint.md#65-persistencia),
  [§6.6](../architecture/blueprint.md#66-migraciones-del-esquema) y
  [§6.8](../architecture/blueprint.md#68-copias).
- Catálogo: REG-08 en [§1](../requirements/catalog.md#1-reglas-que-no-se-pueden-romper-requisitos-transversales);
  los requisitos MIG retirados en [§2.30](../requirements/catalog.md#230-mig--migración-desde-la-v1-retirada);
  la decisión D1 en [§6.2](../requirements/catalog.md#62-decisiones-del-usuario).
- Paquete de diseño: [LEEME-VINCULANTE](../design/handoff/LEEME-VINCULANTE.md).
- ADR relacionados: [ADR-0007](0007-documento-json-versionado.md) (su parte v1 queda sustituida),
  [ADR-0011](0011-formato-i18n.md) (formato de i18n; la receta gana la sección `retired`).
