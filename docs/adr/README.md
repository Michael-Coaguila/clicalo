# Registro de decisiones de arquitectura (ADR)

Esta carpeta guarda las decisiones **difíciles de revertir** de Clícalo, en formato
[MADR 4](https://adr.github.io/madr/) y en español. Las decisiones reversibles (número de dispatchers,
modo de publicación, herramientas, estrategia de pruebas…) viven en las páginas de
[`docs/architecture/`](../architecture/README.md) y se cambian con un PR normal.

## Cuándo hace falta un ADR

Según [§13 del plano](../architecture/blueprint.md#13-convenciones-de-ingeniería), un cambio necesita un
ADR si toca:

- un límite de confianza (IPC, elevación, actualizaciones, firma, contenido importado);
- un formato persistido o un contrato público (documento, `usage.json`, formato para compartir, manifiesto,
  CLI, `clicalo://`, IPC);
- el framework de UI;
- el modelo de procesos o de estado;
- la licencia;
- la firma.

El trabajo `adr` de la CI exige un ADR nuevo o cambiado en esta carpeta cuando un PR toca una ruta de
`architecture/sensitive-paths.json` (error `CLCA010`); en local se comprueba con `cl adr-check --base main`.

## Reglas

1. **Un ADR aceptado no se edita.** Para cambiar una decisión se escribe un ADR nuevo que la sustituya; el
   antiguo pasa a «Sustituido por ADR-NNNN» en un PR que solo cambia ese estado.
2. **El plano no se edita sin un ADR** cuando la decisión afectada tiene uno.
3. **Nunca se rebaja un requisito** en un ADR. Si un requisito parece inviable, se formula una propuesta
   al usuario (ver [cómo leer el catálogo](../requirements/README.md)).
4. **La evidencia se enlaza, no se inventa.** Solo se citan fuentes que figuren en
   [`docs/architecture/decision-record/`](../architecture/README.md#registro-de-la-decisión-tecnológica)
   o fuentes primarias comprobadas.

## Cómo escribir uno

1. Copia [0000-template.md](0000-template.md) como `NNNN-titulo-corto.md`, con el siguiente número libre.
2. Rellena todas las secciones. Si una opción no tiene contras reales, probablemente no se evaluó a fondo.
3. Abre un PR con título `docs: …` (o con el tipo y el ámbito del cambio que motiva el ADR) y enlázalo
   desde el índice de abajo.

## Índice

| ADR | Decisión | Estado | Fecha |
|---|---|---|---|
| [0001](0001-framework-ui-wpf.md) | WPF sobre .NET 10 LTS como framework de UI, condicionado a S1, S3 y S4 | Aceptado | 2026-09-25 |
| [0002](0002-monolito-modular-hexagonal.md) | Monolito modular hexagonal con 8 ensamblados | Aceptado | 2026-09-25 |
| [0003](0003-cuatro-duenos-de-estado.md) | Cuatro dueños de estado inmutable y UI proyectada por funciones puras | Aceptado | 2026-09-25 |
| [0004](0004-motor-ledger-valla-y-sentinel.md) | Motor funcional, *ledger* con escritura adelantada, valla de generación y Sentinel | Aceptado | 2026-09-25 |
| [0005](0005-superficies-no-activables-y-foreground-orchestrator.md) | Superficies no activables y un único `ForegroundOrchestrator` | Aceptado | 2026-09-25 |
| [0006](0006-capa-de-punteros-propia.md) | Capa de punteros propia y seguimiento del puntero externo | Aceptado | 2026-09-25 |
| [0007](0007-documento-json-versionado.md) | Documento JSON versionado, escritura atómica y uso aparte | Aceptado | 2026-09-25 |
| [0008](0008-secretos-dpapi-y-administrador-de-credenciales.md) | Secretos con DPAPI y el Administrador de credenciales | Aceptado | 2026-09-25 |
| [0009](0009-elevacion-y-componente-de-sistema.md) | Elevación como proceso completo y componente de sistema opcional | Aceptado | 2026-09-25 |
| [0010](0010-ipc-minima.md) | IPC mínima: solo `Show`, `OpenUri` e `ImportFile` | Aceptado | 2026-09-25 |
| [0011](0011-formato-i18n.md) | JSON plano con marcadores con nombre, plurales CLDR y claves tipadas | Aceptado | 2026-09-25 |
| [0012](0012-velopack-canales-y-datos.md) | Velopack, canales, `packId Clicalo.App` y datos en `%AppData%\Clicalo` | Aceptado | 2026-09-25 |
| [0013](0013-firma-de-codigo-y-manifiesto-firmado.md) | Authenticode y manifiesto firmado con llave de hardware fuera de GitHub | Aceptado | 2026-09-25 |
| [0014](0014-ia-con-clave-propia.md) | IA solo con clave propia; proxy de cuota diferido | Aceptado | 2026-09-25 |
| [0015](0015-licencia-mit-y-dco.md) | Licencia MIT y DCO | Aceptado | 2026-09-25 |
| [0016](0016-soporte-de-windows-10.md) | Soporte completo de Windows 10 22H2 en la 2.x con revisión en 2027 | Aceptado | 2026-09-25 |
| [0017](0017-sin-plugins-de-codigo.md) | Sin *plugins* de código: extensibilidad solo por datos | Aceptado | 2026-09-25 |
| [0018](0018-contratos-de-sentinel-ledger-y-envoltorio.md) | Contratos de M2: arranque de Sentinel, *ledger* v2 y envoltorio del documento 1.0 | Propuesto | 2026-09-26 |

El hito M0 exige expresamente los ADR 0001, 0002 y 0015
([§14 del plano](../architecture/blueprint.md#14-hoja-de-ruta-por-hitos)); el resto recoge las demás
decisiones iniciales de [§16](../architecture/blueprint.md#16-adrs-iniciales).

## Decisiones condicionadas a spikes

Algunas decisiones se aceptaron con una condición medible. Si el spike falla, se aplica lo previsto en la
columna de la derecha.

| Spike (hito M1) | ADR afectados | Si falla |
|---|---|---|
| S1 · No activación | 0001, 0005 | Se reabre ADR-0001 |
| S2 · Punteros, gestos y dispatchers | 0006 | Si solo falla la medida con un dispatcher, se separan los roles; si falla lo demás, se reabre ADR-0006 |
| S3 · UIA sobre ventana no activable | 0001 | Modo voz; si tampoco funciona, se reabre ADR-0001 |
| S4 · Texto y primer plano por origen | 0001, 0005 | Origen de voz: modo teclado y voz; toque: se reabre ADR-0001 |
| S8 · Distribución y firma | 0012, 0013 | Proveedor alternativo o ADR nuevo |
| S9 · Guardián, *ledger* y valla | 0004 | Se replantea ADR-0004 |
| S14 · Componente de sistema | 0009 | Propuesta P4-B al usuario |
