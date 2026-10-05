# Contratos públicos entre ejecutables

Esta página recoge los contratos de línea de órdenes que unen los ejecutables de Clícalo. Son contratos públicos
(`architecture/sensitive-paths.json`): cambiarlos exige un ADR, y romper uno ya publicado, una versión *major*. La
decisión de cada uno está en su ADR; aquí está el detalle exacto que fijan las pruebas.

Por ahora solo existen los contratos entre `Clicalo.exe` y `Clicalo.Sentinel.exe`
([ADR-0018](../adr/0018-contratos-de-sentinel-ledger-y-envoltorio.md)). La línea de órdenes de `Clicalo.exe`, los
enlaces `clicalo://` y la IPC entre instancias ([ADR-0010](../adr/0010-ipc-minima.md)) se documentarán aquí cuando su
código exista.

## Arranque de `Clicalo.Sentinel.exe`

Lo escribe `SentinelSupervisor` (`Platform.Windows/SentinelHost`) y lo lee Sentinel; los dos compilan el mismo
`Clicalo.Platform.Core.Guardian.SentinelStartInfo`, compatible con Native AOT. Sentinel se publica junto a
`Clicalo.exe` y nunca se mezcla con otra versión.

**Protocolo 2** (desde la decisión D3 del usuario, 2026-10-03). Siete argumentos, en este orden, con cultura invariable
y sin espacios:

| # | Argumento | Valor | Origen |
|---|---|---|---|
| 1 | `--protocol=2` | Versión del contrato. Sentinel rechaza cualquier otra, también la 1 | `SentinelStartInfo.ProtocolVersion` |
| 2 | `--parent=0x<hex>` | *Handle* heredado del principal (`SYNCHRONIZE \| PROCESS_QUERY_LIMITED_INFORMATION`) | `GuardianHandles.DuplicateCurrentProcessForChild` |
| 3 | `--ledger=0x<hex>` | *Handle* heredado del *ledger* v2, en solo lectura | `KeyLedgerSection.DuplicateForGuardian` |
| 4 | `--pipe=0x<hex>` | *Handle* heredado del extremo de lectura del pipe del latido | `GuardianHandles.CreateHeartbeatPipe` |
| 5 | `--heartbeat-ms=<n>` | Periodo del latido y del reintento de un soltado rechazado, en ms (> 0) | `Timings.Guardian.PipeHeartbeatInterval` |
| 6 | `--crash-loop=<n>/<ms>` | Fallos dentro de la ventana que llevan al modo seguro | `Timings.App.CrashLoop` |
| 7 | `--refused-release-wait-ms=<n>` | Cuánto se reintenta, como mucho, un soltado rechazado por algo distinto del escritorio seguro, en ms (> 0) | `Timings.Guardian.RefusedReleaseWait` |

- Se heredan **exactamente tres** *handles*, los de los argumentos 2 a 4, con `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`.
- Un argumento que falte, sobre, esté fuera de su sitio o no tenga el formato hace que Sentinel salga con
  `InvalidArguments` (3) sin tocar nada; el principal lo relanza con `Timings.Guardian.RestartBackoff`.
- El protocolo 1 (seis argumentos, sin el 7) se retiró al añadir el reintento del soltado: un Sentinel 1 con un
  principal 2, o al revés, se rechazan entre sí.

### Qué hace Sentinel con ese contrato

1. Espera, sin consumir CPU, a que el principal termine o el pipe se rompa. Un pipe roto con el principal vivo es un
   cierre a propósito: espera cinco latidos a que el principal termine y, si no, sale con `CleanExit` sin soltar.
2. Lee el *ledger* (si no es la versión 2, sale con `LedgerUnreadable`) y envía el lote de liberación en menos de
   200 ms (S9).
3. Si `SendInput` no acepta todo el lote, vuelve a enviar lo que no salió en cada latido (argumento 5), sin separar
   nunca la máscara de menú de su Alt o Win:
   - sin límite mientras el rechazo sea del escritorio seguro (nada aceptado y `ERROR_ACCESS_DENIED`: sesión
     bloqueada, UAC, Ctrl+Alt+Supr u otro escritorio de entrada);
   - como mucho el tiempo del argumento 7 de rechazos seguidos de otro tipo; después deja de soltar;
   - si la sesión termina mientras tanto, Windows termina Sentinel con ella y no se relanza nada.
4. Solo después del último soltado decide el relanzamiento con `RelaunchPolicy` (marcas del *ledger* y diario de
   fallos), de modo que el proceso nuevo nunca pulsa una tecla que Sentinel vaya a soltar después.

## Relanzamiento de `Clicalo.exe` por Sentinel

Sentinel lanza el `Clicalo.exe` de su carpeta sin *handles* heredados (`GuardianProcess.Start`) y con estos
argumentos (`CrashJournal.RelaunchArguments`):

| Argumento | Cuándo | Significado |
|---|---|---|
| `--after-crash=<ms Unix>` | Siempre | Instante UTC de la muerte del principal (no el del soltado). El proceso relanzado lo añade a `%LocalAppData%\Clicalo\crash-journal.json` con `CrashJournal.Append` |
| `--safe-mode` | Si este fallo alcanza `Timings.App.CrashLoop` | El proceso relanzado arranca en modo seguro |

No relanza si el *ledger* tiene `CleanShutdown` o `NoRelaunch` (salida limpia, actualización o traspaso elevado).

## Códigos de salida de Sentinel

`SentinelExitCode` (`Platform.Core/Guardian`). Nadie los lee todavía; sirven para el diagnóstico y las pruebas.

| Código | Nombre | Significado |
|---|---|---|
| 0 | `CleanExit` | Salida limpia sin nada que soltar, o pipe cerrado a propósito con el principal vivo |
| 1 | `ReleasedAndRelaunched` | Soltó y relanzó el principal |
| 2 | `ReleasedWithoutRelaunch` | Soltó y no relanzó (marcas) o el relanzamiento falló |
| 3 | `InvalidArguments` | Los argumentos no siguen el protocolo 2 |
| 4 | `LedgerUnreadable` | El *handle* no es un *ledger* v2 |
