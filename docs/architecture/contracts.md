# Contratos públicos entre ejecutables

Esta página recoge los contratos de línea de órdenes que unen los ejecutables de Clícalo. Son contratos públicos
(`architecture/sensitive-paths.json`): cambiarlos exige un ADR, y romper uno ya publicado, una versión *major*. La
decisión de cada uno está en su ADR; aquí está el detalle exacto que fijan las pruebas.

Por ahora solo existen los contratos entre `Clicalo.exe` y `Clicalo.Sentinel.exe`
([ADR-0022](../adr/0022-guardian-simple.md), que sustituye al protocolo 2 de
[ADR-0018](../adr/0018-contratos-de-sentinel-ledger-y-envoltorio.md)). La línea de órdenes de `Clicalo.exe`, los
enlaces `clicalo://` y la IPC entre instancias ([ADR-0010](../adr/0010-ipc-minima.md)) se documentarán aquí cuando su
código exista.

## Arranque de `Clicalo.Sentinel.exe`

Lo escribe `SentinelSupervisor` (`Platform.Windows/SentinelHost`) y lo lee Sentinel; los dos compilan el mismo
`Clicalo.Platform.Core.Guardian.SentinelStartInfo`, compatible con Native AOT. Sentinel se publica junto a
`Clicalo.exe` y nunca se mezcla con otra versión.

**Protocolo 3** (desde [ADR-0022](../adr/0022-guardian-simple.md), 2026-10-05). Cuatro argumentos, en este orden, con
cultura invariable y sin espacios:

| # | Argumento | Valor | Origen |
|---|---|---|---|
| 1 | `--protocol=3` | Versión del contrato. Sentinel rechaza cualquier otra, también la 1 y la 2 | `SentinelStartInfo.ProtocolVersion` |
| 2 | `--parent=0x<hex>` | *Handle* heredado del principal (`SYNCHRONIZE \| PROCESS_QUERY_LIMITED_INFORMATION`) | `GuardianHandles.DuplicateCurrentProcessForChild` |
| 3 | `--retry-ms=<n>` | Cada cuánto se reintenta un soltado que Windows rechaza, en ms (> 0) | `Timings.Guardian.ReleaseRetryInterval` |
| 4 | `--crash-loop=<n>/<ms>` | Fallos dentro de la ventana que llevan al modo seguro | `Timings.App.CrashLoop` |

- Se hereda **exactamente un** *handle*, el del argumento 2, con `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`.
- Un argumento que falte, sobre, esté fuera de su sitio o no tenga el formato hace que Sentinel salga con
  `InvalidArguments` (3) sin tocar nada; el principal lo relanza con `Timings.Guardian.RestartBackoff`.
- El principal comprueba cada `Timings.Guardian.WatchInterval` que Sentinel sigue vivo. No hay *pipe* ni latido: para
  retirar a Sentinel basta con que el principal termine con código 0.

### Qué hace Sentinel con ese contrato

1. Espera, sin consumir CPU, a que el principal termine (`WaitForSingleObject`) y lee su código de salida. Si no
   puede esperar sobre el *handle*, sale con `InvalidArguments` sin soltar nada: el principal podría seguir vivo.
2. Pregunta a Windows qué está pulsado (`GetAsyncKeyState` de `0x01` a `0xFE`) y lo suelta todo en un lote: los botones
   primero, las teclas en modo VK con el código de exploración de `MapVirtualKey` (y `KEYEVENTF_EXTENDEDKEY` si es
   extendido), los modificadores al final y la máscara de menú (`VK 0xE8`) antes de cada Alt o Win.
3. Si no puede leer el estado (el escritorio de entrada no es el suyo: sesión bloqueada, UAC, Ctrl+Alt+Supr) o
   `SendInput` no acepta todo el lote, lo intenta otra vez cada `--retry-ms`, sin límite (decisión D3 del usuario),
   leyendo cada vez el estado de nuevo. Si la sesión termina mientras tanto, Windows termina Sentinel con ella y no se
   relanza nada.
4. Solo después del soltado decide el relanzamiento con `RelaunchPolicy` (código de salida y diario de fallos), de modo
   que el proceso nuevo nunca pulsa una tecla que Sentinel vaya a soltar después.

## Relanzamiento de `Clicalo.exe` por Sentinel

Sentinel lanza el `Clicalo.exe` de su carpeta sin *handles* heredados (`GuardianProcess.Start`) y con estos
argumentos (`CrashJournal.RelaunchArguments`):

| Argumento | Cuándo | Significado |
|---|---|---|
| `--after-crash=<ms Unix>` | Siempre | Instante UTC de la muerte del principal (no el del soltado). El proceso relanzado lo añade a `%LocalAppData%\Clicalo\crash-journal.json` con `CrashJournal.Append` |
| `--safe-mode` | Si este fallo alcanza `Timings.App.CrashLoop` | El proceso relanzado arranca en modo seguro |

No relanza si el principal salió con código 0 (salida limpia, fin de la sesión, actualización o traspaso elevado), ni
si el fallo supera `Timings.App.CrashLoop` (también falló el relanzamiento en modo seguro dentro de la ventana): suelta
y sale con `ReleasedWithoutRelaunch`.

## Códigos de salida de Sentinel

`SentinelExitCode` (`Platform.Core/Guardian`). Nadie los lee todavía; sirven para el diagnóstico y las pruebas.

| Código | Nombre | Significado |
|---|---|---|
| 0 | `CleanExit` | El principal salió con código 0: soltó lo que quedara y no relanzó |
| 1 | `ReleasedAndRelaunched` | Salida anómala: soltó y relanzó el principal |
| 2 | `ReleasedWithoutRelaunch` | Salida anómala: soltó, pero no relanzó (el relanzamiento falló o se superó el bucle de fallos) |
| 3 | `InvalidArguments` | Los argumentos no siguen el protocolo 3, o no puede esperar sobre el *handle* del principal |
