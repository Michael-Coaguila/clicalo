---
status: Propuesto
date: 2026-09-26
decision-makers: Michael Coaguila (mantenedor)
consulted: ADR-0004, ADR-0007, plano §3.1, §6.5 y §7.4; contratos de M2
informed: paquetes de M2, mediante docs/testing/spikes/M2-ownership.md
---

# ADR-0018 · Contratos de M2: arranque de Sentinel, *ledger* v2 y envoltorio del documento 1.0

## Contexto y planteamiento del problema

[ADR-0004](0004-motor-ledger-valla-y-sentinel.md) decidió el motor funcional, el *ledger* con escritura adelantada,
la valla de generación y Sentinel; [ADR-0007](0007-documento-json-versionado.md), el documento JSON versionado con
escritura atómica. Ninguno fija el **contrato exacto** entre dos ejecutables (`Clicalo.exe` y `Clicalo.Sentinel.exe`)
ni la forma exacta de la versión del documento, y M2 los construye en cinco paquetes a la vez. Son contratos públicos o
formatos persistidos (rutas de `architecture/sensitive-paths.json`): cambiarlos después obliga a coordinar dos
ejecutables o a migrar datos.

¿Qué contratos se fijan antes de implementar, de modo que el motor, Sentinel y la persistencia no puedan divergir?

## Factores de decisión

- REG-03, SEG-006 y SEG-007: Sentinel debe soltar exactamente lo pulsado aunque el principal muera.
- Sentinel es Native AOT y depende solo de `Platform.Core`: no puede leer `timings.json` (se genera en Domain) ni
  cargar configuración (un archivo más es superficie de ataque y E/S en el camino de soltar).
- REG-08 y DAT-003: un documento de un *major* futuro nunca se sobrescribe; uno de un *minor* posterior se lee.
- El proyecto aún no se ha publicado: estos contratos se pueden fijar sin migración.

## Opciones consideradas

- Contratos explícitos en código compartido, con constantes y pruebas (`Platform.Core` e `Infrastructure`)
- Sentinel lee un archivo de configuración o `timings.json`
- Nombres globales (memoria compartida o pipe con nombre) en lugar de *handles* heredados
- Versión entera del documento (`"schema": 2`, como el paquete de diseño)

## Resultado de la decisión

Opción elegida: **«Contratos explícitos en código compartido»**, porque ambos ejecutables compilan el mismo código de
`Platform.Core`, las pruebas fijan cada byte y cada argumento, y Sentinel no hace E/S de configuración.

1. **Arranque de Sentinel** (`Clicalo.Platform.Core.Guardian.SentinelStartInfo`). El principal lanza Sentinel con
   `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` y **exactamente tres** *handles* heredados: el proceso padre
   (`SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION`), el *ledger* en solo lectura y su extremo de un pipe anónimo. La
   línea de órdenes lleva, en este orden y con cultura invariable: `--protocol=1`, `--parent=<handle hex>`,
   `--ledger=<handle hex>`, `--pipe=<handle hex>`, `--heartbeat-ms=<entero>` (`Timings.Guardian.PipeHeartbeatInterval`)
   y `--crash-loop=<n>/<ms>` (`Timings.App.CrashLoop`). Sentinel rechaza cualquier otra versión de protocolo y sale con
   `InvalidArguments`; el principal lo relanza con su espera (`Timings.Guardian.RestartBackoff`). Sentinel se publica
   junto a `Clicalo.exe` y nunca se mezcla con otra versión.
2. **Ledger v2** (`Clicalo.Platform.Core.KeyLedger.KeyLedgerLayout`): la sección sin nombre de 4 KiB con el diseño de
   [§7.4 del plano](../architecture/blueprint.md#74-registro-de-pulsadas-el-lógico-y-el-físico), byte a byte: `Magic`
   `'CLKL'`, `LayoutVersion = 2`, marcas, secuencia, latido, generación, 128 ranuras de 8 bytes desde `0x020` y los
   botones del mouse en `0x420`. Sentinel rechaza otra versión (`LedgerUnreadable`) y el soltado preventivo del
   siguiente arranque cubre ese caso. Cualquier cambio del diseño es `LayoutVersion = 3` y un ADR nuevo.
3. **Envoltorio 1.0** (`Clicalo.Infrastructure.Persistence`): `clicalo.json`, `usage.json` y cada copia llevan
   `"format"` (`clicalo.document` o `clicalo.usage`) y `"schema": { "major": 1, "minor": 0 }`, con `writtenBy`, `seq`,
   `writtenAtUtc`, `payloadSha256` (SHA-256 hexadecimal en minúsculas de la serialización compacta y determinista del
   `payload`) y `payload`. Un *minor* mayor se lee conservando los campos desconocidos; un *major* mayor entra en solo
   lectura.
4. **Clave canónica persistida** (`CanonicalChord.ToStableString`, en `dupIgnored`): texto estable e independiente de la
   cultura que hace ida y vuelta con `TryParse`; su gramática exacta la fija el paquete `domain` con una prueba de ida y
   vuelta antes de la primera beta.

### Consecuencias

- Buena, porque el motor, Sentinel y las pruebas de «muerte en cada paso» usan las mismas constantes y el mismo lector.
- Buena, porque Sentinel sigue sin E/S de configuración y con solo tres *handles*.
- Buena, porque el documento admite cambios aditivos sin romper la vuelta a N−1 (ADR-0007).
- Mala, porque los umbrales de Sentinel viajan por la línea de órdenes: si `timings.json` cambia, Sentinel recibe el
  valor nuevo en el siguiente arranque, no antes.
- Mala, porque la gramática de la clave canónica queda abierta hasta la primera beta.

### Confirmación

- `tests/Clicalo.Sentinel.Tests/KeyLedgerLayoutTests` fija el diseño de §7.4 y los tres *handles*.
- Ida y vuelta de `SentinelStartInfo` y rechazo de otra versión (paquete `engine`); S9 en la CI.
- `tests/Clicalo.Infrastructure.Tests/Persistence` fija el esquema 1.0 y, con el paquete `persistence`, la lectura de un
  *minor* posterior y el rechazo de un *major* futuro.

## Pros y contras de las opciones

### Contratos explícitos en código compartido

- Buena, porque una sola fuente compila en los dos ejecutables.
- Mala, porque exige disciplina de versiones en `Platform.Core`.

### Configuración leída por Sentinel

- Buena, porque los umbrales se podrían cambiar sin relanzar.
- Mala, porque añade E/S y un archivo manipulable al guardián, y otro formato que mantener.

### Nombres globales

- Buena, porque no hace falta heredar *handles*.
- Mala, porque cualquier proceso del usuario podría ocupar el nombre o leer el *ledger* (ADR-0004 ya lo descartó).

### Versión entera del documento

- Buena, porque es más simple.
- Mala, porque no distingue un cambio aditivo de uno incompatible (ADR-0007 ya lo descartó).

## Criterios de reapertura

- Si S9 demuestra que el diseño del *ledger* o el arranque de Sentinel no cumplen su criterio.
- Si alguna vez hiciera falta que Sentinel y el principal sean de versiones distintas (por ejemplo, actualizar uno sin el
  otro).

## Más información

- Plano: [§3.1](../architecture/blueprint.md#31-vista-de-procesos),
  [§6.5](../architecture/blueprint.md#65-persistencia), [§7.4](../architecture/blueprint.md#74-registro-de-pulsadas-el-lógico-y-el-físico).
- Desviación: [D-20](../architecture/deviations.md#d-20--contratos-de-m2).
- ADR relacionados: [ADR-0004](0004-motor-ledger-valla-y-sentinel.md), [ADR-0007](0007-documento-json-versionado.md).
