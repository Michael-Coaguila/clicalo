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
   lectura. El `payload` 1.0 lo describen `data/schemas/document.schema.json` y `data/schemas/usage.schema.json`, y los
   *fixtures* inmutables de `tests/Clicalo.Infrastructure.Tests/Fixtures/schema/1.0/` lo validan:
   - `settings`, `always`, `profiles`, `frequents`, `dupIgnored` y `onboarding`; los ajustes con los nombres de docs/02.
   - Una tecla es su id de `keys.json`, con `@left` o `@right` cuando nombra un lado; `keySafety.maxHoldMs` 0 es
     «Nunca» y el límite de un atajo es `inherit`, `never` o `after` con `maxHoldMs`.
   - El destino de una app es `{kind: exe | store | document | raw}`; una web con `raw: true` es una dirección que no
     es http ni https.
   - Un texto en reposo es `{enc: "dpapi.v1", blob, len, private}` (DPAPI del usuario con la entropía fija
     `Clicalo.Text.v1`, ADR-0008), o las marcas `{enc: "unavailable"}` y `{enc: "excluded"}` (COP-005). No se usa el
     prefijo `dpapi:` del paquete de diseño.
   - El uso (`usage.json`) guarda instantes en milisegundos Unix por atajo, con las claves en orden ordinal y el
     `usageEpoch` del documento; una copia de seguridad lleva además el uso de su momento.
   - Un perfil compartido (`clicalo-perfil-<id>.json`, DAT-007) es `{type: "profile-share", schema, writtenBy,
     writtenAtUtc, profile}` con el mismo `profile` del documento, sin envoltorio con *hash*; es contenido no confiable
     y al importarlo se sustituyen todos sus ids.
4. **Clave canónica persistida** (`CanonicalChord.ToStableString`, en `dupIgnored`). Gramática fija, independiente de la
   cultura:
   - Los *tokens* van unidos por `+`. Primero los modificadores, cada uno como mucho una vez y en este orden: `ctrl`,
     `lctrl`, `rctrl`, `alt`, `lalt`, `altgr`, `shift`, `lshift`, `rshift`, `win`, `lwin`, `rwin`.
   - Después las teclas principales, en orden, como su `KeyId` en minúsculas, con `%` escrito `%25` y `+` escrito `%2B`.
     Una tecla principal cuyo texto coincide con un modificador lleva escapado su primer carácter (por ejemplo
     `%6Cwin`).
   - `TryParse` solo acepta exactamente el texto que escribe `ToStableString` (ni mayúsculas ni escapes innecesarios),
     de modo que una clave persistida tiene un único texto. Lo cubre una propiedad de ida y vuelta de 10 000 casos.
5. **Relanzamiento tras un fallo.** Sentinel no escribe archivos (`banned-api-exceptions.json`). Cuando el principal
   muere sin `CleanShutdown` ni `NoRelaunch`, suelta primero y después lanza el `Clicalo.exe` de su carpeta, sin
   *handles* heredados, con `--after-crash=<ms Unix>` y, si se alcanzó `Timings.App.CrashLoop`, `--safe-mode`
   (`CrashJournal.RelaunchArguments`). Lee `%LocalAppData%\Clicalo\crash-journal.json` solo para decidir el bucle de
   fallos, en el formato de `Platform.Core/Guardian/CrashJournal` (`format: clicalo.crash-journal`, `version: 1`, hasta
   32 instantes ISO 8601); el principal relanzado añade el fallo con `CrashJournal.Append` a través de la escritura
   atómica.

### Consecuencias

- Buena, porque el motor, Sentinel y las pruebas de «muerte en cada paso» usan las mismas constantes y el mismo lector.
- Buena, porque Sentinel sigue sin E/S de configuración y con solo tres *handles*.
- Buena, porque el documento admite cambios aditivos sin romper la vuelta a N−1 (ADR-0007).
- Mala, porque los umbrales de Sentinel viajan por la línea de órdenes: si `timings.json` cambia, Sentinel recibe el
  valor nuevo en el siguiente arranque, no antes.
- Mala, porque el diario de fallos lo escribe el principal relanzado: si Sentinel no consigue relanzarlo, ese fallo no
  cuenta para el bucle.
- Pendiente de decidir: si Sentinel reintenta las liberaciones que rechaza el escritorio seguro. Hoy, si el principal
  muere con la sesión bloqueada, Sentinel suelta una vez y sale, y el soltado preventivo del relanzamiento también
  corre bloqueado; alguna tecla podría seguir pulsada al desbloquear (docs/testing/spikes/S9.md, «Qué queda abierto»).

### Confirmación

- `tests/Clicalo.Sentinel.Tests/KeyLedgerLayoutTests` fija el diseño de §7.4 y los tres *handles*.
- Ida y vuelta de `SentinelStartInfo` y rechazo de otra versión (paquete `engine`); S9 en la CI.
- `tests/Clicalo.Infrastructure.Tests/Persistence` fija el esquema 1.0 y, con el paquete `persistence`, la lectura de un
  *minor* posterior y el rechazo de un *major* futuro; `tests/Clicalo.Data.Tests` valida los *fixtures* 1.0 con
  `document.schema.json` y `usage.schema.json`.
- `tests/Clicalo.Domain.Tests/Keys/CanonicalChordTests` fija la gramática de la clave canónica y su ida y vuelta.
- `tests/Clicalo.Sentinel.Tests` y `tests/Clicalo.App.Tests/AppOptionsTests` fijan los argumentos del relanzamiento.

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
